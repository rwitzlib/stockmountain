using Xunit;
using Xunit.Abstractions;

namespace MarketViewer.Infrastructure.UnitTests.Performance
{
    public class PerformanceUnitTests
    {
        private readonly ITestOutputHelper _output;

        public PerformanceUnitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Read_And_Serialize_S3_Backtest_File_JsonSerializer()
        {
            //var sp = new Stopwatch();
            //sp.Start();

            //using var client = new AmazonS3Client(Amazon.RegionEndpoint.USEast2);

            //var response = await client.GetObjectAsync(new GetObjectRequest
            //{
            //    BucketName = "lad-dev-marketviewer",
            //    Key = "backtest/2023/01/aggregate_1_hour"
            //});

            //using var reader = new StreamReader(response.ResponseStream);
            //var json = await reader.ReadToEndAsync();

            //var aggregates = JsonSerializer.Deserialize<List<StocksResponse>>(json);

            //sp.Stop();
            //_output.WriteLine($"Serialized {aggregates.Count} results in {sp.ElapsedMilliseconds}ms");

            //sp.ElapsedMilliseconds.Should().BeLessThan(30000);
            //aggregates.Should().NotBeNull();
        }

        [Fact]
        public async Task Read_And_Serialize_S3_Backtest_File_MemoryPack()
        {
            //var sp = new Stopwatch();
            //sp.Start();

            //using var client = new AmazonS3Client(Amazon.RegionEndpoint.USEast2);

            //var response = await client.GetObjectAsync(new GetObjectRequest
            //{
            //    BucketName = "lad-dev-marketviewer",
            //    Key = "backtest/2023/01/aggregate_1_hour"
            //});

            //using var streamReader = new MemoryStream();
            //response.ResponseStream.CopyTo(streamReader);
            //var result = streamReader.ToArray();

            //var test = new List<StocksResponse>
            //{
            //    new StocksResponse
            //    {
            //        Ticker = "asdf",
            //        Results = new List<Polygon.Client.Models.Bar>
            //        {
            //            new Polygon.Client.Models.Bar
            //            {
            //                Close = 1234
            //            }
            //        }
            //    }
            //};
            //var bytes = MemoryPackSerializer.Serialize(test);

            //var asdf = MemoryPackSerializer.Deserialize<List<StocksResponse>>(bytes);

            //var mp = MemoryPackStreamingSerializer.DeserializeAsync<List<StocksResponse>>(response.ResponseStream, bufferAtLeast: 1800000000, readMinimumSize: 1800000000);
            //var aggregates = mp.ToBlockingEnumerable().First();

            //sp.Stop();
            //_output.WriteLine($"Serialized {aggregates.Count} results in {sp.ElapsedMilliseconds}ms");

            //sp.ElapsedMilliseconds.Should().BeLessThan(15000);
            //aggregates.Should().NotBeNull();
        }
    }
}
