using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Management.Configuration;
using Management.Enums;
using Management.Requests;
using Management.Response;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

namespace Management.Middleware;

[ExcludeFromCodeCoverage]
public class PermissionMiddleware(IAmazonSimpleSystemsManagement ssmClient, AwsConfigs configuration, ILogger<PermissionMiddleware> logger, RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(await GenerateErrorResponse(context));
                return;
            }

            var token = authorizationHeader.ToString().Split(" ").Last();

            var response = await ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = configuration.DeployTokenName,
                WithDecryption = true
            });

            if (response.HttpStatusCode != HttpStatusCode.OK || response.Parameter is null || response.Parameter.Value != token)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(await GenerateErrorResponse(context));
                return;
            }
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {ex}", e.Message);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync(await GenerateErrorResponse(context));
            return;
        }

        await next(context);
    }

    private static async Task<string> GenerateErrorResponse(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var request = JsonSerializer.Deserialize<DeployRequest>(await reader.ReadToEndAsync());
        var deployResponse = new DeployResponse
        {
            Id = request.Id,
            Status = DeployStatus.Failed,
            ErrorMessages = ["Internal Server Error."]
        };
        var json = JsonSerializer.Serialize(deployResponse);

        return json;
    }
}
