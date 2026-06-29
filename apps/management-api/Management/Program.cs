using Amazon;
using Amazon.DynamoDBv2;
using Amazon.ECR;
using Amazon.SimpleSystemsManagement;
using Management.Configuration;
using Management.Middleware;
using Management.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: false, reloadOnChange: true);

var configs = builder.Configuration.GetSection("AwsConfigs").Get<AwsConfigs>();

builder.Services.AddSingleton(configs)
    .AddSingleton<DeployService>()
    .AddSingleton<DatabaseService>()
    .AddSingleton<PermissionMiddleware>()
    .AddSingleton<IAmazonECR>(client => new AmazonECRClient(RegionEndpoint.USEast2))
    .AddSingleton<IAmazonDynamoDB>(client => new AmazonDynamoDBClient(RegionEndpoint.USEast2))
    .AddSingleton<IAmazonSimpleSystemsManagement>(client => new AmazonSimpleSystemsManagementClient(RegionEndpoint.USEast2));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.EnvironmentName == "local")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<PermissionMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();