using MarketViewer.Api.Authentication;
using MarketViewer.Api.Authorization;
using MarketViewer.Api.Config;
using MarketViewer.Api.Healthcheck;
using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Hubs;
using MarketViewer.Api.Jobs;
using MarketViewer.Api.Middleware;
using MarketViewer.Api.Services;
using MarketViewer.Application.DependencyInjection;
using MarketViewer.Application.Handlers.Market;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Converters;
using MarketViewer.Core.DependencyInjection;
using MarketViewer.Core.Services;
using MarketViewer.Filters;
using MarketViewer.Infrastructure.DependencyInjection;
using MarketViewer.Infrastructure.Services;
using MarketViewer.Studies.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Rewrite;

namespace MarketViewer.Api;

public class Program
{
    [ExcludeFromCodeCoverage]
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        DotNetEnv.Env.Load($"../../../../{builder.Environment.EnvironmentName}.env");
        DotNetEnv.Env.Load($"../../../{builder.Environment.EnvironmentName}.env");
        builder.Configuration.AddEnvironmentVariables();

        var microserviceApplicationAssemblies = new[]
        {
            typeof(StocksHandler).Assembly,
            typeof(MarketDataRepository).Assembly,
            typeof(IBacktestRepository).Assembly
        };

        builder.Services.AddMediatR(q => q.RegisterServicesFromAssemblies(microserviceApplicationAssemblies))
            .AddMemoryCache(options => options.TrackStatistics = true)
            .AddHttpClient()
            .RegisterStudies()
            .RegisterApplication()
            .RegisterCore(builder.Configuration)
            .RegisterInfrastructure(builder.Configuration)
            .AddHttpContextAccessor()
            .AddSignalR();

        builder.Services.AddSingleton<IndicatorExpressionEngine>();
        builder.Services.AddSingleton<TickerCache>();
        builder.Services.AddSingleton<ScannerCache>();
        builder.Services.AddSingleton<CacheWarmupState>();
        builder.Services.AddSingleton<BarCacheService>();
        builder.Services.Configure<ClerkWebhookConfig>(builder.Configuration.GetSection("ClerkWebhook"));
        builder.Services.PostConfigure<ClerkWebhookConfig>(options =>
        {
            options.SigningSecret = builder.Configuration["CLERK_WEBHOOK_SIGNING_SECRET"]
                ?? builder.Configuration["ClerkWebhook:SigningSecret"]
                ?? string.Empty;
        });
        builder.Services.AddScoped<ClerkWebhookVerifier>();

        builder.Services.AddHostedService<CacheWarmupService>();

        builder.Services.AddQuartz()
            .AddQuartzHostedService(opt =>
            {
                opt.WaitForJobsToComplete = true;
            });

        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.Converters.Add(new IndicatorConverter());
            options.JsonSerializerOptions.Converters.Add(new IndicatorPointConverter());
        });

        var signingKeyCache = new SigningKeyCache();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "http://auth.stockmountain.io",
                    ValidAudience = "react",
                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        var keys = signingKeyCache.GetKeys();
                        return new JsonWebKeySet(keys).GetSigningKeys();
                    }
                };
            });

        // Register the authorization handler and policy
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddScoped<IAuthorizationHandler, RequiredPermissionsHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, MetricsBearerTokenHandler>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, RequiredPermissionsAuthorizationPolicyProvider>();
        builder.Services.AddAuthorization();

        builder.Services.AddHttpLogging(options => options.LoggingFields = HttpLoggingFields.All);

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks()
            .AddCheck<PingHealthCheck>("Ping", tags: ["healthcheck"]);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = true;
        }); 
        
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("MarketViewer.Api"))
            .WithMetrics(metrics => 
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAWSInstrumentation()
                    .AddPrometheusExporter()
                    .AddMeter("MarketViewer.Market")
                    .AddMeter("Microsoft.AspNetCore.Hosting"))
            .WithTracing(tracing =>
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddQuartzInstrumentation()
                    .AddAWSInstrumentation())
            .UseOtlpExporter();

        var app = builder.Build();

        app.UseHttpLogging();

        app.UseRewriter(new RewriteOptions().AddRewrite(@"^api/(.*)", "$1", skipRemainingRules: true));

        if (app.Environment.IsEnvironment("dev") || app.Environment.IsEnvironment("local"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(policy => policy
           .AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader()
           .SetPreflightMaxAge(TimeSpan.FromHours(2)));

        app.MapPrometheusScrapingEndpoint().RequireAuthorization(q => q.AddRequirements(new MetricsBearerTokenRequirement()));

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = q => q.Tags.Contains("healthcheck"),
            ResponseWriter = async (context, report) =>
            {
                var result = report.Entries.All(check => check.Value.Status == HealthStatus.Healthy);
                await context.Response.WriteAsync(result ? "Healthy" : "Unhealthy");
            }
        });

        app.UseWebSockets();
        app.UseRouting();

        app.UseAuthentication();
        app.UseMiddleware<AuthContextMiddleware>();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chathub");
        app.MapControllers();

        app.Run();
    }
}
