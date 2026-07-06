namespace MarketViewer.Api.Config;

public class ClerkAuthConfig
{
    /// <summary>
    /// Clerk instance issuer URL, e.g. https://your-instance.clerk.accounts.dev (dev)
    /// or https://clerk.stockmountain.io (production).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Origins allowed as the token's azp (authorized party) claim. Clerk session tokens
    /// carry no audience, so azp is what ties a token to our frontend. Empty list skips the check.
    /// </summary>
    public string[] AuthorizedParties { get; set; } = [];
}
