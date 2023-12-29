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

// TODO cleanup unnecessary methods
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

        public Task<ScanResponse> GetAllFromTableAsync(string tableName)
        {
            var conditions = new List<string>();

            var result = _client.ScanAsync(tableName, conditions);

            return result;
        }

        public async Task<bool> InsertAircraftAsync(RecognizedAircraft aircraft)
        {
            
            var planeId = aircraft.TransponderId;
            var callsign = aircraft.Callsign;
            var estimationCount = aircraft.Positions.Where(position => position.IsEstimated).Count();
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
                    {"Position", new AttributeValue { S=position.ToString()} },
                    {"Estimations", new AttributeValue {N=estimationCount.ToString()} },
                    {"Callsign", new AttributeValue { S=callsign} },
                    {"ExpireAt", new AttributeValue{N=DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString() } }
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

        public async Task<bool> DeleteAircraftAsync(RecognizedAircraft aircraft)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["ID"] = new AttributeValue { S = aircraft.TransponderId },
            };

            var request = new DeleteItemRequest
            {
                TableName = "RecognizedAirPicture",
                Key = key,
            };

            var response = await _client.DeleteItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
    }
}