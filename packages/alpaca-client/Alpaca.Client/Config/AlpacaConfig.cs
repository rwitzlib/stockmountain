using System.Diagnostics.CodeAnalysis;

namespace Alpaca.Client.Config;

[ExcludeFromCodeCoverage]
public class AlpacaConfig
{
    /// <summary>
    /// Paper trading API base URL. Clock/calendar reads and the AlpacaPaper execution
    /// tier both use this environment.
    /// </summary>
    public string BaseUrl { get; set; } = "https://paper-api.alpaca.markets/";
    public string ApiKeyId { get; set; } = string.Empty;
    public string ApiSecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Live trading environment, used only by the AlpacaLive execution tier. Keys may be
    /// left empty until live trading is enabled; the live client then fails auth rather
    /// than silently trading the wrong account.
    /// </summary>
    public string LiveBaseUrl { get; set; } = "https://api.alpaca.markets/";
    public string LiveApiKeyId { get; set; } = string.Empty;
    public string LiveApiSecretKey { get; set; } = string.Empty;
}
