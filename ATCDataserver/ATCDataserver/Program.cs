using Amazon.DynamoDBv2;
using Amazon.Runtime.CredentialManagement;
using DynamoDBClient;
using RecognizedAirPicture;

namespace ATCDataserver
{
    /// <summary>
    /// Runs the aggregation, extrapolation and outlier removal.
    /// Will upload the aircrafts to dynamodb
    /// </summary>
    public class DataServerMain
    {
        private static readonly int NEW_ESTIMATE_THRESHOLD_IN_SECONDS = 5; // in seconds
        private static readonly int REMOVE_OLD_AIRCRAFT_THRESHOLD = 2; // in minutes

        private static readonly int MAX_MESSAGE_FIELDS = 22;

        private static readonly int MAX_ALLOWED_ESTIMATES = 15;

        private static readonly object AIR_PICTURE_LOCK = new object();

        private static List<RecognizedAircraft> _airPicture = new List<RecognizedAircraft>();

        private static readonly string SERVICE_IP = "141.79.10.172";
        private static readonly int SERVICE_PORT = 30003;

        private static readonly string SERVICE_PROFILE = "atc";

        public static void Main()
        {            
            var receiver = new DataReceiver(SERVICE_IP, SERVICE_PORT);
            receiver.DataReceived += HandleReceivedData;
            
            var dataStreamTask = Task.Run(() => receiver.StreamReceive());


            var profiles = new CredentialProfileStoreChain();
            var requestedProfileName = SERVICE_PROFILE;
            AmazonDynamoDBClient awsClient;
            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://127.0.0.1:8000"
            };
            if (!profiles.TryGetAWSCredentials(requestedProfileName, out var awsCredentials))
            {
                // profile does not exist
                // in this case it will try to use the default and if it does not work
                // it will try to take the instance profile service on EC2
                awsClient = new AmazonDynamoDBClient(clientConfig);
            }
            else
            {
                // profile exists
                awsClient = new AmazonDynamoDBClient(awsCredentials, clientConfig);
            }
            var client = new DynamoClientRAP(awsClient);

            while (true) 
            {
                ManageDataIteration(_airPicture);   
                UploadChangedAircrafts(client, _airPicture);
            }
        }

        // Manage the data such as calculating new positions using extrapolation
        // and marking aircrafts as obsolete when they have not received updates in a while.
        public static void ManageDataIteration(List<RecognizedAircraft> airPicture)
        {
            List<RecognizedAircraft> orderedAircrafts;

            lock (AIR_PICTURE_LOCK)
            {
                orderedAircrafts = airPicture.OrderBy(aircraft => aircraft.LastMessageReceived).ToList();
            }
            foreach (var aircraft in orderedAircrafts)
            {

                var latestAircraftMessageTime = aircraft.LastMessageReceived;
                var minutes = (DateTime.UtcNow - latestAircraftMessageTime).TotalMinutes;

                if (minutes > REMOVE_OLD_AIRCRAFT_THRESHOLD || aircraft.EstimationsSinceLastActualPosition > MAX_ALLOWED_ESTIMATES)
                {
                    lock (AIR_PICTURE_LOCK)
                    {
                        airPicture.Remove(aircraft);
                    }
                    // DynamoDb automatically deletes the uploaded version once it expires
                    // no need to do further processing on aircraft
                    continue;
                }

                var lastKnownPosition = aircraft.GetLastPosition();


                if (lastKnownPosition != null &&
                    (DateTime.UtcNow - lastKnownPosition.Generated).TotalSeconds > NEW_ESTIMATE_THRESHOLD_IN_SECONDS &&
                    aircraft.HasValidState())
                {
                    aircraft.AddNewEstimatePosition();
                }
                
            }
        }

        public static void UploadChangedAircrafts(DynamoClientRAP client, IEnumerable<RecognizedAircraft> airPicture)
        {
            List<RecognizedAircraft> changedAircrafts;

            lock (AIR_PICTURE_LOCK)
            {
                changedAircrafts = airPicture.Where(aircraft => aircraft.HasChanged && aircraft.HasValidState()).ToList();
            }

            var taskList = new List<Task>();
            foreach (var aircraft in changedAircrafts)
            {
                var task = client.InsertAircraftAsync(aircraft);
                aircraft.HasChanged = false;                  
                taskList.Add(task);
            }
            Task.WaitAll(taskList.ToArray());
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

        public static void HandleReceivedData(string receivedMessage)
        {
            if (receivedMessage == string.Empty)
            {
                return;
            }
            var sbsMessage = DataParser(receivedMessage);
            ProcessMessage(_airPicture, sbsMessage);
        }

        private static void ProcessMessage(List<RecognizedAircraft> airPicture, SBSMessageHelper sbsMessage)
        {
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

            lock (AIR_PICTURE_LOCK)
            {
                aircraft = airPicture.FirstOrDefault(aircraft => aircraft.TransponderId.Equals(sbsMessage.FieldHexIdent));

                if (aircraft == null)
                {
                    aircraft = new RecognizedAircraft(sbsMessage);
                    airPicture.Add(aircraft);
                    return; // after creating aircraft from a single message Aggregation is not needed.
                }
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
                        };

                        aircraft.AddNewPosition(newPosition);
                    }
                    break;
                case SBSMessageHelper.ESAirborneVelocityMessage:

                    aircraft.Track = sbsMessage.FieldTrack;
                    aircraft.GroundSpeed = sbsMessage.FieldGroundSpeed;
                    break;
                default:
                    break;
            }
            aircraft.LastMessageReceived = sbsMessage.FieldDateMessageGenerated.ToDateTime(sbsMessage.FieldTimeMessageGenerated);
            aircraft.HasChanged = true;
        }
    }
}
