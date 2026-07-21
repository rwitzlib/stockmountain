using System.Diagnostics;
using System.Text.Json;

namespace MarketViewer.Infrastructure.Logging;

/// <summary>
/// Canonical log line ("wide event"): one context-rich JSON event per invocation/request,
/// emitted exactly once from a finally block. See plans/09-canonical-logging.md.
/// Cost fields are estimates (they exclude billed cold-start INIT time and are lost when
/// the runtime dies); the platform REPORT record is the billing-exact source of truth.
/// </summary>
public sealed class WideEvent
{
    // us-east-2 x86_64 on-demand pricing as of mid-2026. Dashboards keep their own copy as
    // a Grafana variable; this one only feeds the per-event estimate.
    private const double CostPerGbSecondUsd = 0.0000166667;
    private const double CostPerRequestUsd = 0.0000002;

    private static bool _coldStart = true;

    private readonly Dictionary<string, object?> _fields;
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private readonly int _memoryMb;
    private bool _emitted;

    private WideEvent(string service)
    {
        var isColdStart = _coldStart;
        _coldStart = false;

        _ = int.TryParse(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE"), out _memoryMb);

        _fields = new Dictionary<string, object?>
        {
            ["event"] = "canonical",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["service"] = service,
            ["function_name"] = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"),
            ["region"] = Environment.GetEnvironmentVariable("AWS_REGION"),
            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            ["commit"] = Environment.GetEnvironmentVariable("COMMIT_SHA"),
            ["cold_start"] = isColdStart,
            ["memory_mb"] = _memoryMb,
            ["outcome"] = "success"
        };
    }

    public static WideEvent Start(string service) => new(service);

    /// <summary>Adds or overwrites a field. Use snake_case names; keep values JSON-primitive.</summary>
    public WideEvent Set(string name, object? value)
    {
        _fields[name] = value;
        return this;
    }

    public WideEvent SetRequestId(string awsRequestId) => Set("aws_request_id", awsRequestId);

    public WideEvent SetError(Exception exception)
    {
        return Set("outcome", "error")
            .Set("error_type", exception.GetType().Name)
            .Set("error_message", exception.Message);
    }

    /// <summary>
    /// Serializes and writes the event as a single JSON line to stdout. Idempotent so a
    /// finally block can call it unconditionally.
    /// </summary>
    public void Emit()
    {
        if (_emitted)
        {
            return;
        }
        _emitted = true;

        var duration = Stopwatch.GetElapsedTime(_startTimestamp);
        _fields["duration_ms"] = Math.Round(duration.TotalMilliseconds, 1);
        _fields["level"] = _fields["outcome"] as string == "error" ? "error" : "info";

        if (_memoryMb > 0)
        {
            var gbSeconds = duration.TotalSeconds * _memoryMb / 1024d;
            _fields["est_gb_seconds"] = Math.Round(gbSeconds, 4);
            _fields["est_cost_usd"] = Math.Round(gbSeconds * CostPerGbSecondUsd + CostPerRequestUsd, 8);
        }

        Console.WriteLine(JsonSerializer.Serialize(_fields));
    }
}
