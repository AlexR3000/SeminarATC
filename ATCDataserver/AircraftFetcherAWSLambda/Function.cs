using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AircraftFetcherAWSLambda;

public class Function
{
    private static readonly string? _dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT_DEBUG");
    /// <summary>
    /// A simple function that scans the RecognizedAirPicture table from dynamodb
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public APIGatewayProxyResponse FunctionHandler(ILambdaContext context)
    {
        try
        {
            AmazonDynamoDBClient client;
            if (_dynamoDbEndpoint is not null)
            {
                var config = new AmazonDynamoDBConfig
                {
                    ServiceURL = _dynamoDbEndpoint
                };
                client = new AmazonDynamoDBClient(config);
            }
            else
            {
                client = new AmazonDynamoDBClient();
            }
            var table = Table.LoadTable(client, "RecognizedAirPicture");

            var search = table.Scan(new ScanFilter());

            var items = search.GetRemainingAsync().Result;

            


            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            var resultList = new List<Aircraft>();
            foreach (var item in items)
            {
                item.TryGetValue("Estimations", out DynamoDBEntry estimate);
                item.TryGetValue("ID", out DynamoDBEntry id);
                item.TryGetValue("Latitude", out DynamoDBEntry latitude);
                item.TryGetValue("Longitude", out DynamoDBEntry longitude);
                item.TryGetValue("PositionCreated", out DynamoDBEntry positionCreated);
                item.TryGetValue("Callsign", out DynamoDBEntry callsign);
                item.TryGetValue("ExpireAt", out DynamoDBEntry expiration);
                item.TryGetValue("Track", out DynamoDBEntry track);
                var aircraft = new Aircraft
                {
                    Estimates = estimate.AsInt(),
                    ID = id.AsString(),
                    Latitude = latitude.AsString(),
                    Longitude = longitude.AsString(),
                    PositionCreated = positionCreated.AsString(),
                    Track = track.AsInt(),
                    Callsign = callsign.AsString()

                };
                resultList.Add(aircraft);
            }

            var encodedJson = JsonSerializer.Serialize(resultList);
            var decodedJson = Regex.Unescape(encodedJson);

            var response = new APIGatewayProxyResponse()
            {
                StatusCode = 200,
                Body = decodedJson,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
            };
            return response;
        }
        catch (Exception ex) 
        {
            var response = new APIGatewayProxyResponse()
            {
                StatusCode = 500,
                Body = ex.Message,
            };
            return response;
        }
    }
}
