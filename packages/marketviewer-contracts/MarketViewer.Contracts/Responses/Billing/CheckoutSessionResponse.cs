namespace MarketViewer.Contracts.Responses.Billing;

public class CheckoutSessionResponse
{
    /// <summary>Client secret for mounting the embedded Checkout session in the web app.</summary>
    public string ClientSecret { get; set; }
}
