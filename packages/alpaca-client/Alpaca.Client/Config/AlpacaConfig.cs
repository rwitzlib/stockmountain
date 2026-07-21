using System.Diagnostics.CodeAnalysis;

namespace Alpaca.Client.Config;

[ExcludeFromCodeCoverage]
public class AlpacaConfig
{
    /// <summary>
    /// Trading API base URL. Defaults to the paper environment; set to
    /// https://api.alpaca.markets/ for live trading.
    /// </summary>
    public string BaseUrl { get; set; } = "https://paper-api.alpaca.markets/";
    public string ApiKeyId { get; set; } = string.Empty;
    public string ApiSecretKey { get; set; } = string.Empty;
}
