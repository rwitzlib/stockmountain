using Xunit;
using Amazon.Lambda.TestUtilities;
using AutoFixture;
using System.Threading.Tasks;
using Moq.AutoMock;
using Amazon.S3;
using Moq;
using Amazon.S3.Model;
using Polygon.Client.Interfaces;

namespace Kesha.UnitTests;

public class FunctionTest
{
    private readonly IFixture _autoFixture;
    private readonly AutoMocker _autoMocker;

    public FunctionTest()
    {
        _autoFixture = new Fixture();
        _autoMocker = new AutoMocker();
    }

    [Theory]
    [InlineData("2023-09-25")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Invalid_Input_Should_Return_Immediately(string date)
    {
        var function = _autoMocker.CreateInstance<Function>();
        var context = new TestLambdaContext();

        await function.FunctionHandler(new TickerDetailsRequest
        {
            Date = date
        }, context);

        _autoMocker.GetMock<IAmazonS3>().Verify(q => q.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never());
        _autoMocker.GetMock<IPolygonClient>().Verify(q => q.GetTickers(It.IsAny<Polygon.Client.Requests.PolygonGetTickersRequest>()), Times.Never());
        _autoMocker.GetMock<IPolygonClient>().Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Never());
    }
}
