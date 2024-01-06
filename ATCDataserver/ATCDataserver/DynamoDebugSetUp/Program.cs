using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using System.Linq.Expressions;

namespace DynamoDbSetup
{
    public class Program
    {
        public static void Main()
        {
            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();
            clientConfig.ServiceURL = "http://127.0.0.1:8000";


            AmazonDynamoDBClient client = new AmazonDynamoDBClient(clientConfig);


            try
            {
                Console.WriteLine("Deleting old table if exists");
                var deleteResult = DeleteDynamo(client);

                while (deleteResult.IsCompleted != true)
                {

                }
            }
            catch (Exception)
            {
                Console.WriteLine("No deletion");
            }

            var result = CreateDynamoTable(client);

            while (result.IsCompleted != true) { }

            var enableTTLResult = EnableTTLAsync(client, "RecognizedAirPicture");

            while (enableTTLResult.IsCompleted != true) { }

        }

        static async Task EnableTTLAsync(IAmazonDynamoDB client, string tableName)
        {
            var ttlSpecification = new TimeToLiveSpecification
            {
                AttributeName = "ExpireAt", // Replace with the attribute name you want to use for TTL
                Enabled = true
            };

            var request = new UpdateTimeToLiveRequest
            {
                TableName = tableName,
                TimeToLiveSpecification = ttlSpecification
            };

            var response = await client.UpdateTimeToLiveAsync(request);

            Console.WriteLine($"TTL Status for table '{tableName}': {response.TimeToLiveSpecification.Enabled}");
        }

        public static async Task<bool> CreateDynamoTable(AmazonDynamoDBClient client)
        {

            try
            {
                var response = await client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = "RecognizedAirPicture",
                    AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "ID",
                        AttributeType = ScalarAttributeType.S
                    },

                },
                    KeySchema = new List<KeySchemaElement>()
                {
                    new KeySchemaElement
                    {
                        AttributeName = "ID",
                        KeyType = "HASH",
                    },
                    
                },
                    ProvisionedThroughput = new ProvisionedThroughput { ReadCapacityUnits = 40000, WriteCapacityUnits = 40000 },
                });



                Console.WriteLine("Waiting for table to become active");

                var request = new DescribeTableRequest
                {
                    TableName = response.TableDescription.TableName
                };



                TableStatus status;
                int sleepDuration = 2000;

                do
                {
                    System.Threading.Thread.Sleep(sleepDuration);
                    var describeTableResponse = await client.DescribeTableAsync(request);
                    status = describeTableResponse.Table.TableStatus;
                }
                while (status != "ACTIVE");

                return status == TableStatus.ACTIVE;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            

        }

        public static async Task<DeleteTableResponse> DeleteDynamo(AmazonDynamoDBClient client)
        {
            var request = new DescribeTableRequest
            {
                TableName = "RecognizedAirPicture"
            };

            TableStatus status;

            var describeTableResponse = await client.DescribeTableAsync(request);
            status = describeTableResponse.Table.TableStatus;

            try
            {
                if (status != "ACTIVE")
                {
                    throw new Exception("Table is not active");
                }

                var requestDelete = new DeleteTableRequest
                {
                    TableName = "RecognizedAirPicture"
                };

                return await client.DeleteTableAsync(requestDelete);
            }
            catch (Exception)
            {
                throw;
            }

        }
    }
}