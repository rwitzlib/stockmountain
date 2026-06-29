using Amazon;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Market.Backtest;
using System.Net;
using Environment = System.Environment;

namespace Backtest.Lambda.UnitTests;

public class DispatcherUnitTests
{
    private readonly TestLambdaContext _context = new TestLambdaContext();
    private readonly DispatcherFunction _classUnderTest;

    public DispatcherUnitTests()
    {
        Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));

        Environment.SetEnvironmentVariable("MEMORY", "2048");

        using var client = new AmazonSimpleSystemsManagementClient(RegionEndpoint.USEast2);
        var response = client.GetParameterAsync(new GetParameterRequest
        {
            Name = "/tokens/polygon",
            WithDecryption = true
        }).Result;

        if (response.HttpStatusCode == HttpStatusCode.OK)
        {
            Environment.SetEnvironmentVariable("POLYGON_TOKEN", response.Parameter.Value);
        }

        _classUnderTest = new DispatcherFunction();
    }

    [SkippableFact]
    public async Task Test()
    {
        // Arrange
        var request = new OrchestratorRequest
        {
            Start = DateTimeOffset.Parse("2025-11-01"),
            End = DateTimeOffset.Parse("2025-11-01"),
            EntrySettings = new StrategyEntrySettings
            {
                Filters =
                [
                    "close > 505 [1m]"
                ]
            }
        };

        // Act & Assert
        //await _classUnderTest.FunctionHandler(request, _context);
    }
}
