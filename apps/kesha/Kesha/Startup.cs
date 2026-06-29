using Amazon;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Polygon.Client.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Kesha
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        public static IServiceProvider ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<IAmazonS3, AmazonS3Client>(client => new AmazonS3Client(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast2,
                //ServiceURL = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME")
            }));

            serviceCollection.AddPolygonClient(Environment.GetEnvironmentVariable("POLYGON_TOKEN"));

            return serviceCollection.BuildServiceProvider();
        }
    }
}
