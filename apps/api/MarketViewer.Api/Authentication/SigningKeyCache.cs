using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Api.Authentication;

[ExcludeFromCodeCoverage]
public class SigningKeyCache
{
    public string Keys { get; set; }

    public void InitializeKeys(string url)
    {
        var client = new HttpClient();
        var keys = client.GetStringAsync(url).Result;

        Keys = keys;
    }

    public string GetKeys()
    {
        if (string.IsNullOrWhiteSpace(Keys))
        {
            InitializeKeys("https://auth.stockmountain.io/.well-known/jwks.json");
        }

        return Keys;
    }
}
