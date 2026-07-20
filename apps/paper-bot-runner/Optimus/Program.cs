using Amazon;
using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Optimus.Adapter;
using Optimus.Adapter.DependencyInjection;
using Optimus.Authentication;
using Optimus.Authorization;
using Optimus.HostedServices;
using Optimus.Infrastructure.DependencyInjection;
using Optimus.Services;
using Massive.Client.DependencyInjection;
using Quartz;
using SchwabApi.DependencyInjection;
using System.Text.Json.Serialization;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        // Directory should be formatted like this
        // | local.env
        // | stockmountain
        // |_
        //   | stockmountain-optimus
        //   | stockmountain-marketviewer
        //   | ...
        DotNetEnv.Env.Load($"../../../{builder.Environment.EnvironmentName}.env");
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddLogging()
            .AddMemoryCache()
            .AddHttpContextAccessor()
            .AddSingleton<SellWorker>()
            .AddSingleton<TradeExecutionService>()
            .AddSingleton<UnrealizedPnlService>()
            .AddMassiveClient(
                Environment.GetEnvironmentVariable("MASSIVE_TOKEN")
                ?? builder.Configuration.GetSection("Tokens").GetValue<string>("MassiveApi"));

        builder.Services.AddHttpClient<UnrealizedPnlService>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration.GetSection("Urls").GetValue<string>("MarketViewer"));
        });

        // Hosted services
        builder.Services.AddHostedService<CacheWarmupService>();
        builder.Services.AddHostedService<ScanQueueConsumer>();

        // Register AWS Services
        builder.Services.AddSingleton<IAmazonSimpleNotificationService>(client => new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast2))
            .AddSingleton<IAmazonDynamoDB>(client => new AmazonDynamoDBClient(RegionEndpoint.USEast2))
            .AddSingleton<IAmazonSQS>(client => new AmazonSQSClient(RegionEndpoint.USEast2));

        builder.Services.RegisterInfrastructure(builder.Configuration);
        builder.Services.RegisterAdapters(builder.Configuration);
        builder.Services.RegisterSchwabClients();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
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
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, RequiredPermissionsAuthorizationPolicyProvider>();
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(); // This line is now valid

        builder.Services.AddQuartz(q =>
            {
                // Unrealized P/L Refresh Job - runs every 5 minutes during market hours
                var pnlJobKey = new JobKey("UnrealizedPnlJob");
                q.AddJob<UnrealizedPnlWorker>(opts => opts.WithIdentity(pnlJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(pnlJobKey)
                    .WithIdentity("UnrealizedPnlTrigger")
                    .WithCronSchedule("0 0/5 9-20 ? * MON-FRI", x => x
                        .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))));

                var sellWorkerJobKey = new JobKey("SellWorkerJob");
                q.AddJob<SellWorker>(opts => opts.WithIdentity(sellWorkerJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(sellWorkerJobKey)
                    .WithIdentity("SellWorkerTrigger")
                    .WithCronSchedule("0 0/1 9-20 ? * MON-FRI", x => x
                        .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))));
            })
            .AddQuartzHostedService(opt =>
            {
                opt.WaitForJobsToComplete = true;
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsEnvironment("dev") || app.Environment.IsEnvironment("local"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        app.UseWebSockets();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}