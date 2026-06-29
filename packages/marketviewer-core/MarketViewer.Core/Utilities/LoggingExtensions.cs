using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Core.Utilities;

public static class LoggingExtensions
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public static string CorrelationId
    {
        get => _correlationId.Value ?? Guid.NewGuid().ToString();
        set => _correlationId.Value = value;
    }

    public static IDisposable BeginOperationScope(this ILogger logger, string operation, string? userId = null, object? additionalContext = null)
    {
        var correlationId = CorrelationId;
        var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["Operation"] = operation,
            ["UserId"] = userId,
            ["Timestamp"] = DateTimeOffset.UtcNow
        });

        if (additionalContext != null)
        {
            logger.LogInformation("Starting operation {Operation} with context {@Context}", operation, additionalContext);
        }
        else
        {
            logger.LogInformation("Starting operation {Operation}", operation);
        }

        return scope;
    }

    public static async Task<T> LogOperationAsync<T>(
        this ILogger logger,
        string operation,
        Func<Task<T>> operationFunc,
        string? userId = null,
        object? additionalContext = null)
    {
        var stopwatch = Stopwatch.StartNew();

        using (logger.BeginOperationScope(operation, userId, additionalContext))
        {
            try
            {
                var result = await operationFunc();
                stopwatch.Stop();

                logger.LogInformation("Operation {Operation} completed successfully in {DurationMs}ms",
                    operation, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                logger.LogError(ex, "Operation {Operation} failed after {DurationMs}ms: {Message}",
                    operation, stopwatch.ElapsedMilliseconds, ex.Message);

                throw;
            }
        }
    }

    public static T LogOperation<T>(
        this ILogger logger,
        string operation,
        Func<T> operationFunc,
        string? userId = null,
        object? additionalContext = null)
    {
        var stopwatch = Stopwatch.StartNew();

        using (logger.BeginOperationScope(operation, userId, additionalContext))
        {
            try
            {
                var result = operationFunc();
                stopwatch.Stop();

                logger.LogInformation("Operation {Operation} completed successfully in {DurationMs}ms",
                    operation, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                logger.LogError(ex, "Operation {Operation} failed after {DurationMs}ms: {Message}",
                    operation, stopwatch.ElapsedMilliseconds, ex.Message);

                throw;
            }
        }
    }

    public static void LogApiRequest(
        this ILogger logger,
        string controller,
        string action,
        string httpMethod,
        string? userId = null,
        object? requestData = null)
    {
        logger.LogInformation("API Request: {Controller}.{Action} [{Method}] UserId: {UserId}",
            controller, action, httpMethod, userId ?? "anonymous");

        if (requestData != null)
        {
            logger.LogDebug("Request data: {@RequestData}", requestData);
        }
    }

    public static void LogApiResponse(
        this ILogger logger,
        string controller,
        string action,
        int statusCode,
        long durationMs,
        bool success = true)
    {
        if (success)
        {
            logger.LogInformation("API Response: {Controller}.{Action} completed with status {StatusCode} in {DurationMs}ms",
                controller, action, statusCode, durationMs);
        }
        else
        {
            logger.LogWarning("API Response: {Controller}.{Action} failed with status {StatusCode} in {DurationMs}ms",
                controller, action, statusCode, durationMs);
        }
    }

    public static void LogBusinessEvent(
        this ILogger logger,
        string eventType,
        string entityType,
        string entityId,
        string? userId = null,
        object? eventData = null)
    {
        logger.LogInformation("Business Event: {EventType} on {EntityType} {EntityId} by User {UserId}",
            eventType, entityType, entityId, userId ?? "system");

        if (eventData != null)
        {
            logger.LogDebug("Event data: {@EventData}", eventData);
        }
    }
}
