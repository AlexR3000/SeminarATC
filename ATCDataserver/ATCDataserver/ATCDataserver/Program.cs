using ATCDataserver;
using DynamoDBClient;
using RecognizedAirPicture;
using System.Collections.Concurrent;

namespace ATCDataserver
{
    public class DataServerMain
    {
        private static readonly int NEW_ESTIMATE_THRESHOLD = 1;
        private static readonly int REMOVE_OLD_AIRCRAFT_THRESHOLD = 1;

        private static readonly int MAX_MESSAGE_FIELDS = 22;

        public static void Main()
        {
            var airPicture = new List<RecognizedAircraft>();
            var aircraftForRemoval = new List<RecognizedAircraft>();

            var receiver = new DataReceiver("141.79.10.172", 30003);

            
            var dataStreamTask = Task.Run(() => receiver.StreamReceive());

            while (true) 
            {
                var hasReceived = receiver.ReceivedMessageQueue.TryDequeue(out var receivedMessage);

                if (!hasReceived || receivedMessage == null)
                {
                    continue;
                }
                var sbsMessage = DataParser(receivedMessage);
                ProcessMessage(airPicture, sbsMessage);

                ManageDataIteration(airPicture);

                var client = new DynamoClientRAP();
                UploadChangedAircrafts(client, airPicture);

            }
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

        public static void ManageDataIteration(List<RecognizedAircraft> airPicture)
        {
            List<RecognizedAircraft> orderedAircrafts;

            orderedAircrafts = airPicture.OrderBy(aircraft => aircraft.LastMessage).ToList();

            foreach (var aircraft in orderedAircrafts)
            {

                var latestAircraftMessageTime = aircraft.LastMessage;
                var minutes = (DateTime.UtcNow - latestAircraftMessageTime).TotalMinutes;

                if (minutes > REMOVE_OLD_AIRCRAFT_THRESHOLD)
                {
                    airPicture.Remove(aircraft);
                    // DynamoDb automatically deletes the uploaded version once it expires
                    // no need to do further processing on aircraft
                    continue;
                }

                var lastKnownPosition = aircraft.GetLastPosition();


                if (lastKnownPosition != null &&
                    (DateTime.UtcNow - lastKnownPosition.Generated).TotalMinutes > NEW_ESTIMATE_THRESHOLD &&
                    aircraft.HasValidState())
                {
                    aircraft.AddNewEstimatePosition();
                }
                
            }
        }

        public static void UploadChangedAircrafts(DynamoClientRAP client, IEnumerable<RecognizedAircraft> airPicture)
        {
            List<RecognizedAircraft> changedAircrafts;

            changedAircrafts = airPicture.Where(aircraft => aircraft.HasChanged && aircraft.HasValidState()).ToList();
            
            foreach (var aircraft in changedAircrafts)
            {
                client.InsertAircraftAsync(aircraft).Wait();
                aircraft.HasChanged = false;                  
            }
        }


        public static SBSMessageHelper DataParser(string message)
        {
            
            // Split when message is null only returns one element with null
            var splitMessage = message.Split(',');

            if (message == string.Empty || splitMessage.Length != MAX_MESSAGE_FIELDS) { return new SBSMessageHelper(ignore: true); }

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
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESAirborneVelocityMessage &&
                sbsMessage.FieldTransmissionType != SBSMessageHelper.ESIdentificationAndCategory)
            {
                return;
            }

            RecognizedAircraft? aircraft;

            aircraft = airPicture.FirstOrDefault(aircraft => aircraft.TransponderId.Equals(sbsMessage.FieldHexIdent));

            if (aircraft == null)
            {
                // no need to lock since aircraft will first receive and then added to the airpicture,
                // meaning it is not subject to being uploaded until added to airPicture list.
                aircraft = new RecognizedAircraft(sbsMessage);
                airPicture.Add(aircraft);
                return; // after creating aircraft from a single message Aggregation is not needed.
            }
            
            AggregateMessage(aircraft, sbsMessage);

            
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

                    aircraft.Track = sbsMessage.FieldTrack;
                    aircraft.GroundSpeed = sbsMessage.FieldGroundSpeed;
                    break;
                default:
                    break;
            }

            aircraft.HasChanged = true;
        }
    }
}
