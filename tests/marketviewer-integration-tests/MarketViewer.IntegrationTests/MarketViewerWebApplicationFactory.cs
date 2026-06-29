using MarketViewer.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace MarketViewer.IntegrationTests;

public class MarketViewerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("dev");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear default configuration sources
            config.Sources.Clear();
        });

        builder.ConfigureServices(services =>
        {
            // Remove Quartz hosted service to avoid background job execution during tests
            var quartzHostedService = services.FirstOrDefault(s => s.ServiceType == typeof(IHostedService) && 
                s.ImplementationType?.Name == "QuartzHostedService");
            if (quartzHostedService != null)
            {
                services.Remove(quartzHostedService);
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }
}


