namespace MarketViewer.Contracts.Responses.Billing;

public class CheckoutSessionResponse
{
    /// <summary>Stripe-hosted Checkout page to redirect the browser to.</summary>
    public string Url { get; set; }
}
