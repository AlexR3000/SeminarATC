using ATCDataserver;
using DynamoDBClient;
using RecognizedAirPicture;

namespace ATCDataserver
{
    public class DataServerMain
    {
        private static int NewEstimateThreshold = 1;

        private const int MAXMESSAGEFIELDS = 22;

        public static void Main()
        {
            var airPicture = new List<RecognizedAircraft>();
            var aircraftForRemoval = new List<RecognizedAircraft>();

            var receiver = new DataReceiver("127.0.0.1", 5678);

            
            var dataStreamTask = Task.Run(() => receiver.StreamReceiveAsync());


            
            var messageProcessing = Task.Run(() =>
            {
                while (true)
                {
                    string receivedMessage = receiver.ReceivedMessageQueue.Count >= 1
                        ? receiver.ReceivedMessageQueue.Dequeue() : string.Empty;

                    var sbsMessage = DataParser(receivedMessage);
                    ProcessMessage(airPicture, sbsMessage);
                }
            });


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
                    (DateTime.UtcNow - lastKnownPosition.Generated).TotalMinutes > NewEstimateThreshold)
                {
                    aircraft.AddNewEstimatePosition();
                }
            }
        }

        public static async void UploadChangedAircrafts(DynamoClientRAP client, IEnumerable<RecognizedAircraft> airPicture)
        {
            var changedAircrafts = airPicture.Where(aircraft => aircraft.HasChanged && aircraft.HasValidState());

            foreach (var aircraft in changedAircrafts)
            {

                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(aircraft.PlaneLock, new TimeSpan(50), ref lockTaken);

                    if (lockTaken)
                    {
                        await client.InsertAircraftAsync(aircraft);
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
            // Returns when messagetype is not whitelisted
            if (sbsMessage is null || sbsMessage.IgnoreMessage) return;
            if (!sbsMessage.FieldType.Equals(SBSMessageHelper.MSG)) return;
            if (sbsMessage.FieldType.Equals(SBSMessageHelper.MSG) &&
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESAirbornePositionMessage && 
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESAirbornePositionMessage &&
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESIdentificationAndCategory)
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
                case SBSMessageHelper.ESIdentificationAndCategory:
                    aircraft.Callsign = sbsMessage.FieldCallsign;
                    break;
                case SBSMessageHelper.ESAirbornePositionMessage:
                    if (!aircraft.IsOutlierPosition(sbsMessage))
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

                    RecognizedAircraft.AddNewValueToFixedSizeCollection(sbsMessage.FieldTrack, aircraft.LastTracks);
                    aircraft.Track = (int)RecognizedAircraft.ApplyMedianFilter(aircraft.LastTracks);

                    RecognizedAircraft.AddNewValueToFixedSizeCollection(sbsMessage.FieldGroundSpeed, aircraft.LastSpeeds);
                    aircraft.GroundSpeed = (int)RecognizedAircraft.ApplyMedianFilter(aircraft.LastSpeeds);
                    break;
                default:
                    break;
            }

            aircraft.HasChanged = true;
        }
    }
}
