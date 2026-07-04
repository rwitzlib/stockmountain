using System.Globalization;

namespace Backtest.Lambda.Utilities;

public static class LambdaEnvironment
{
    /// <summary>
    /// Memory factor used for credit accounting: allocated memory in GB.
    /// Falls back to the Lambda-provided memory size variable, then to the 2 GB baseline.
    /// </summary>
    public static float GetMemoryFactor()
    {
        var memory = Environment.GetEnvironmentVariable("MEMORY")
            ?? Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE");

        return float.TryParse(memory, NumberStyles.Float, CultureInfo.InvariantCulture, out var megabytes)
            ? megabytes / 1024f
            : 2f;
    }
}
