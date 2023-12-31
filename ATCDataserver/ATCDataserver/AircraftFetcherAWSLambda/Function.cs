using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AircraftFetcherAWSLambda;

public class Function
{
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public APIGatewayProxyResponse FunctionHandler(ILambdaContext context)
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://127.0.0.1:8000"
        };
        var client = new AmazonDynamoDBClient(config);

        var table = Table.LoadTable(client, "RecognizedAirPicture");

        var search = table.Scan(new ScanFilter());

        var items = search.GetRemainingAsync().Result;

        var results = JsonSerializer.Serialize(items);

        var response = new APIGatewayProxyResponse()
        {
            StatusCode = 200,
            Body = results,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };

        return response;
    }
}
