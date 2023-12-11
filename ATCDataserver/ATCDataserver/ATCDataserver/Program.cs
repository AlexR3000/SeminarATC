using ATCDataserver;
using DynamoDBClient;
using RecognizedAirPicture;

namespace ATCDataserver
{
    public class DataServerMain
    {

        private const int MAXMESSAGEFIELDS = 22;

        // TODO refactoring for better separation and scalability
        public static void Main()
        {
            var airPicture = new List<RecognizedAircraft>();
            var aircraftForRemoval = new List<RecognizedAircraft>();

            var receiver = new DataReceiver("127.0.0.1", 5678);

            var dataCancellationToken = new CancellationTokenSource();
            var dataStreamTask = Task.Run(() => receiver.StreamReceiveAsync(), dataCancellationToken.Token);


            var messageProcessingCancellationToken = new CancellationTokenSource();
            var messageProcessing = Task.Run(() =>
            {
                while (true)
                {
                    string receivedMessage = receiver.ReceivedMessageQueue.Count >= 1 ? receiver.ReceivedMessageQueue.Dequeue() : string.Empty;
                    var sbsMessage = DataParser(receivedMessage);
                    ProcessMessage(airPicture, sbsMessage);
                }
            }, messageProcessingCancellationToken.Token);


            var dataStoreManager = Task.Run(() =>
            {
                var client = new DynamoClientRAP();
                while (true)
                {
                    DeleteRemovedAircrafts(client, aircraftForRemoval);
                    UploadChangedAircrafts(client, airPicture);
                    Thread.Sleep(100);

                }
            });

            var dataManager = Task.Run(() =>
            {

                ManageData(airPicture, aircraftForRemoval);


            });

            while (true) { }
        }

        public static async void DeleteRemovedAircrafts(DynamoClientRAP client, List<RecognizedAircraft> aircraftsForRemoval) 
        {
            foreach (var aircraft in aircraftsForRemoval) 
            {
                await client.DeleteAircraftAsync(aircraft);
            }
        }

        // Manage the data such as calculating new positions using extrapolation
        // and marking aircrafts as obsolete when they have not received updates in a while.
        public static void ManageData(List<RecognizedAircraft> airPicture, List<RecognizedAircraft> removalList)
        {
            var orderedAircrafts = airPicture.OrderBy(aircraft => aircraft.LastMessage);

            foreach (var aircraft in orderedAircrafts)
            {
                var latestAircraftMessageTime = aircraft.LastMessage;
                var minutes = (DateTime.UtcNow - latestAircraftMessageTime).TotalMinutes;

                if (minutes > 5)
                {
                    removalList.Add(aircraft);
                    airPicture.Remove(aircraft);
                }

                var lastKnownPosition = aircraft.GetLastPosition();

                if (lastKnownPosition != null &&
                    (DateTime.UtcNow - lastKnownPosition.PositionAddedAt).TotalMinutes > 1)
                {
                    aircraft.AddNewEstimatePosition();
                }
            }
        }

        public static async void UploadChangedAircrafts(DynamoClientRAP client, IEnumerable<RecognizedAircraft> airPicture)
        {
            var changedAircrafts = airPicture.Where(aircraft => aircraft.HasChanged);

            foreach (var aircraft in changedAircrafts)
            {

                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(aircraft.PlaneLock, new TimeSpan(50), ref lockTaken);

                    if (lockTaken)
                    {
                        // TODO change parameter into just aircraft
                        // To make it parallel the foreach loop would have to create more tasks
                        await client.InsertAircraftAsync(aircraft.AircraftId, aircraft.Positions, aircraft.Callsign);
                        aircraft.HasChanged = false;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(aircraft.PlaneLock);
                    }
                }
            }
        }


        public static SBSMessageHelper DataParser(string message)
        {
            // Split when message is null only returns one element with null
            var splitMessage = message.Split(',');

            if (message == string.Empty || splitMessage.Length != MAXMESSAGEFIELDS) { return new SBSMessageHelper(ignore: true); }

            var sbsMessage = new SBSMessageHelper();

            sbsMessage.SetFields(splitMessage);

            return sbsMessage;

        }


        public static void ProcessMessage(List<RecognizedAircraft> airPicture, SBSMessageHelper sbsMessage)
        {
            // TODO think of better solution
            // conditions on when to return immediately
            if (sbsMessage is null || sbsMessage.IgnoreMessage) return;
            if (!sbsMessage.FieldType.Equals(SBSMessageHelper.MSG)) return;
            if (sbsMessage.FieldType.Equals(SBSMessageHelper.MSG) &&
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESAirbornePositionMessage && 
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESAirbornePositionMessage)
            {
                return;
            }

            var aircraft = airPicture.FirstOrDefault(aircraft => aircraft.AircraftId.Equals(sbsMessage.FieldAircraftID));

            if (aircraft == null)
            {
                // no need to lock since aircraft will first receive and then added to the airpicture,
                // meaning it is not subject to being uploaded until added to airPicture list.
                aircraft = new RecognizedAircraft(sbsMessage);
                airPicture.Add(aircraft);
                return; // after creating aircraft from a single message Aggregation is not needed.
            }

            lock (aircraft.PlaneLock)
            {
                AggregateMessage(aircraft, sbsMessage);
            }
        }

        public static void AggregateMessage(RecognizedAircraft aircraft, SBSMessageHelper sbsMessage)
        {
            switch (sbsMessage.FieldTransmissionType)
            {
                case SBSMessageHelper.ESAirbornePositionMessage:
                    if (!aircraft.IsOutlierMessage(sbsMessage))
                    {
                        var newPosition = new Position
                        {
                            Latitude = sbsMessage.FieldLatitude,
                            Longitude = sbsMessage.FieldLongitude,
                            Generated = sbsMessage.FieldDateMessageGenerated
                                .ToDateTime(sbsMessage.FieldTimeMessageGenerated)
                                .ToUniversalTime(),
                        };

                        aircraft.Positions.Add(newPosition);
                    }
                    break;
                case SBSMessageHelper.ESAirborneVelocityMessage:
                    // TODO add outlier removal
                    aircraft.Track = sbsMessage.FieldTrack;
                    aircraft.GroundSpeed = sbsMessage.FieldGroundSpeed;
                    break;
                default:
                    break;
            }

            aircraft.HasChanged = true;
        }

        


        // TODO rework or remove
        public static async void RunDataServerAsync()
        {
            var client = new DynamoClientRAP();

            while (true)
            {
                Console.WriteLine("id or exit to leave");

                var id = "";//Console.ReadLine();

                if (id.Equals("exit"))
                {
                    break;
                }

                // var callsign = "";// Console.ReadLine();
                // var latitude = "";//Console.ReadLine();
                // var longitude = "";//Console.ReadLine();


                // await Task.Run(() => client.InsertPlaneAsync(id, callsign, latitude, longitude));

            }

            var result = await client.GetAllFromTableAsync("RecognizedAirPicture");

            foreach (var item in result.Items)
            {
                Console.Write($"ID: {item["ID"].S}, Latitude: {item["Latitude"].S}, Longitude: {item["Longitude"].S}, callsign: {item["Callsign"].S} ");
                Console.WriteLine("");
            }
        }
    }
}
