using MarketViewer.Api.Config;
using Microsoft.Extensions.Options;
using Stripe;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using PortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using PortalSessionService = Stripe.BillingPortal.SessionService;

namespace MarketViewer.Api.Services.Billing;

public class StripeGateway(IOptions<StripeConfig> options) : IStripeGateway
{
    // Lazy so an unset secret key (e.g. local dev without Stripe) only fails billing
    // endpoints, not startup.
    private readonly Lazy<IStripeClient> _client = new(() => new StripeClient(options.Value.SecretKey));

    public async Task<string> CreateCustomer(string userId)
    {
        var customer = await new CustomerService(_client.Value).CreateAsync(new CustomerCreateOptions
        {
            // The Clerk user id on the customer is the webhook fallback for resolving events
            // back to a user when session/subscription metadata is unavailable.
            Metadata = new Dictionary<string, string> { { "userId", userId } }
        });

        return customer.Id;
    }

    public async Task<Customer> GetCustomer(string customerId)
    {
        return await new CustomerService(_client.Value).GetAsync(customerId);
    }

    public async Task<bool> HasLiveSubscription(string customerId)
    {
        // Auto-paging: ListAsync returns a single page, and a live subscription could
        // hide behind a page of dead ones.
        var subscriptions = new SubscriptionService(_client.Value).ListAutoPagingAsync(new SubscriptionListOptions
        {
            Customer = customerId,
            Status = "all"
        });

        await foreach (var subscription in subscriptions)
        {
            if (subscription.Status is not ("canceled" or "incomplete_expired"))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<string> CreateCheckoutSession(CheckoutSessionSpec spec)
    {
        var sessionOptions = new CheckoutSessionCreateOptions
        {
            Customer = spec.CustomerId,
            ClientReferenceId = spec.UserId,
            Mode = spec.IsSubscription ? "subscription" : "payment",
            LineItems =
            [
                new CheckoutSessionLineItemOptions { Price = spec.PriceId, Quantity = 1 }
            ],
            // Rendered inside our own modal; success is handled in-page via onComplete,
            // so there is no return redirect at all.
            UiMode = "embedded",
            RedirectOnCompletion = "never",
            AllowPromotionCodes = true,
            Metadata = new Dictionary<string, string> { { "userId", spec.UserId } }
        };

        if (spec.IsSubscription)
        {
            sessionOptions.SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { { "userId", spec.UserId } }
            };
        }
        else
        {
            sessionOptions.Metadata.Add("pack", spec.PackId);
            sessionOptions.PaymentIntentData = new Stripe.Checkout.SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "userId", spec.UserId },
                    { "pack", spec.PackId }
                }
            };
        }

        var session = await new CheckoutSessionService(_client.Value).CreateAsync(sessionOptions);
        return session.ClientSecret;
    }

    public async Task<string> CreatePortalSession(string customerId, string returnUrl)
    {
        var portalOptions = new PortalSessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        };

        if (!string.IsNullOrEmpty(options.Value.PortalConfigurationId))
        {
            portalOptions.Configuration = options.Value.PortalConfigurationId;
        }

        var session = await new PortalSessionService(_client.Value).CreateAsync(portalOptions);
        return session.Url;
    }
}
