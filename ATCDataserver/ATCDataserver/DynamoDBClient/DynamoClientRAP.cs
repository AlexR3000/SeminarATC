using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using RecognizedAirPicture;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DynamoDBClient
{
    // RAP Recognized Air Picture
    public class DynamoClientRAP
    {

        private AmazonDynamoDBClient _client;
        public DynamoClientRAP()
        {
            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();

            clientConfig.ServiceURL = "http://127.0.0.1:8000";

            _client = new AmazonDynamoDBClient(clientConfig);
        }

        public DynamoClientRAP(AmazonDynamoDBClient client)
        {
            _client = client;
        }


        public async Task<bool> InsertAircraftAsync(RecognizedAircraft aircraft)
        {
            
            var planeId = aircraft.TransponderId;
            var callsign = aircraft.Callsign;
            var estimationCount = aircraft.EstimationsSinceLastActualPosition;
            var position = aircraft.GetLastPosition();

            // in case position doesn't exist don't upload
            if (position == null)
            {
                return false;
            }

            try
            {
                var request = new PutItemRequest
                {
                    TableName = "RecognizedAirPicture",
                    Item = new Dictionary<string, AttributeValue>()
                {
                    { "ID", new AttributeValue { S=planeId} },
                    // string set only can have each value once but since the time is part of the stored string
                    // it should be no issue
                    {"Latitude", new AttributeValue { S=position.Latitude.ToString()} },
                    {"Longitude", new AttributeValue { S=position.Longitude.ToString()} },
                    {"PositionCreated", new AttributeValue{S=position.Generated.ToString() } },
                    {"Estimations", new AttributeValue {N=estimationCount.ToString()} },
                    {"Track", new AttributeValue {N=aircraft.Track.ToString() } },
                    {"Callsign", new AttributeValue { S=callsign} },
                    {"ExpireAt", new AttributeValue{N=DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString() } }
                }
                };

                await _client.PutItemAsync(request);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }
    }
}