using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;



// The function handler that will be called for each Lambda event
var handler = (ILambdaContext context) =>
{
    var config = new AmazonDynamoDBConfig
    {
        ServiceURL = "localhost:8000"
    };
    var client = new AmazonDynamoDBClient(config);

    var table = Table.LoadTable(client, "RecognizedAircrafts");

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
};

// Build the Lambda runtime client passing in the handler to call for each
// event and the JSON serializer to use for translating Lambda JSON documents
// to .NET types.
await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();