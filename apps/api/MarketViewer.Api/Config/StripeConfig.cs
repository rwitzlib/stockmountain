namespace MarketViewer.Api.Config;

public class StripeConfig
{
    /// <summary>Supplied via Stripe__SecretKey env var (terraform → Railway); test-mode key on dev.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Supplied via Stripe__WebhookSigningSecret env var. Webhooks are rejected while unset.</summary>
    public string WebhookSigningSecret { get; set; } = string.Empty;

    /// <summary>Stripe Price ids keyed by "Pro" / "Premium" / "PackSmall" / "PackLarge".</summary>
    public Dictionary<string, string> Prices { get; set; } = [];

    /// <summary>Optional Customer Portal configuration id; Stripe's default portal config is used when empty.</summary>
    public string PortalConfigurationId { get; set; } = string.Empty;

    /// <summary>Web app origin used to build Checkout/Portal return URLs (e.g. https://dev.stockmountain.io).</summary>
    public string ReturnUrlBase { get; set; } = string.Empty;
}

public class TierConfig
{
    public float MonthlyCredits { get; set; }
}

public class PackConfig
{
    public float Credits { get; set; }
}
