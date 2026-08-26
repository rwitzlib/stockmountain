using Stripe;

namespace MarketViewer.Api.Services.Billing;

/// <summary>
/// Thin wrapper around the Stripe API calls the billing flow makes over the network,
/// so controllers and the webhook processor stay unit-testable without Stripe traffic.
/// </summary>
public interface IStripeGateway
{
    /// <summary>Creates a Stripe customer tagged with the Clerk user id; returns the customer id.</summary>
    Task<string> CreateCustomer(string userId);

    Task<Customer> GetCustomer(string customerId);

    /// <summary>Creates an embedded Checkout session and returns its client secret.</summary>
    Task<string> CreateCheckoutSession(CheckoutSessionSpec spec);

    /// <summary>Creates a Customer Portal session and returns its hosted-page URL.</summary>
    Task<string> CreatePortalSession(string customerId, string returnUrl);
}

public class CheckoutSessionSpec
{
    public string UserId { get; set; }
    public string CustomerId { get; set; }
    public string PriceId { get; set; }
    public bool IsSubscription { get; set; }

    /// <summary>Pack id ("PackSmall"/"PackLarge") for one-time purchases; null for subscriptions.</summary>
    public string PackId { get; set; }
}
