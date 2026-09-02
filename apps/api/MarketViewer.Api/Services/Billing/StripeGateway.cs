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

    public async Task<LiveSubscription> GetLiveSubscription(string customerId)
    {
        var listOptions = new SubscriptionListOptions
        {
            Customer = customerId,
            Status = "all"
        };
        // The schedule (when one manages the subscription) carries the scheduled
        // period-end change; expanding it here saves a second round trip.
        listOptions.AddExpand("data.schedule");

        await foreach (var subscription in new SubscriptionService(_client.Value).ListAutoPagingAsync(listOptions))
        {
            if (subscription.Status is "canceled" or "incomplete_expired")
            {
                continue;
            }

            return Project(subscription);
        }

        return null;
    }

    public async Task<ProrationPreview> PreviewImmediateChange(LiveSubscription subscription, string priceId)
    {
        var invoice = await new InvoiceService(_client.Value).CreatePreviewAsync(new InvoiceCreatePreviewOptions
        {
            Customer = subscription.CustomerId,
            Subscription = subscription.Id,
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                Items = [new InvoiceSubscriptionDetailsItemOptions { Id = subscription.ItemId, Price = priceId }],
                ProrationBehavior = "always_invoice"
            }
        });

        return new ProrationPreview { AmountDueCents = invoice.AmountDue, Currency = invoice.Currency };
    }

    public async Task<ImmediateChangeResult> ChangePlanNow(LiveSubscription subscription, string priceId)
    {
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items = [new SubscriptionItemOptions { Id = subscription.ItemId, Price = priceId }],
            // Charge the prorated difference now (the customer.subscription.updated webhook
            // grants the credit delta; the subscription_update invoice is money only).
            ProrationBehavior = "always_invoice",
            // If that charge needs customer action (3-D Secure, a declined card) Stripe keeps
            // the subscription on the old price as a pending update instead of leaving it
            // half-switched; the customer completes payment on the hosted invoice page and
            // Stripe applies the update then.
            PaymentBehavior = "pending_if_incomplete"
        };
        updateOptions.AddExpand("latest_invoice");

        var updated = await new SubscriptionService(_client.Value).UpdateAsync(subscription.Id, updateOptions);

        if (updated.PendingUpdate is null)
        {
            return new ImmediateChangeResult { Applied = true };
        }

        return new ImmediateChangeResult
        {
            Applied = false,
            PaymentUrl = updated.LatestInvoice?.HostedInvoiceUrl
        };
    }

    public async Task<DateTime> SchedulePlanChangeAtPeriodEnd(LiveSubscription subscription, string priceId, string priceInterval)
    {
        var scheduleService = new SubscriptionScheduleService(_client.Value);

        // A schedule can only be rewritten, not stacked: drop a previously scheduled change
        // and rebuild from the subscription's actual current phase.
        if (!string.IsNullOrEmpty(subscription.ScheduleId))
        {
            await scheduleService.ReleaseAsync(subscription.ScheduleId);
        }

        var schedule = await scheduleService.CreateAsync(new SubscriptionScheduleCreateOptions
        {
            FromSubscription = subscription.Id
        });

        // Stripe seeds phase 0 with the current period (start/end match the subscription's
        // period bounds); the whole phase list must be resent on update.
        var currentPhase = schedule.Phases[0];
        var effectiveAt = currentPhase.EndDate;

        await scheduleService.UpdateAsync(schedule.Id, new SubscriptionScheduleUpdateOptions
        {
            // After the new-price phase runs its first interval the schedule lets go and the
            // subscription simply continues renewing on the new price.
            EndBehavior = "release",
            ProrationBehavior = "none",
            Phases =
            [
                new SubscriptionSchedulePhaseOptions
                {
                    StartDate = currentPhase.StartDate,
                    EndDate = currentPhase.EndDate,
                    Items = [new SubscriptionSchedulePhaseItemOptions { Price = subscription.PriceId, Quantity = 1 }],
                    Discounts = MapDiscounts(currentPhase),
                    // Phase metadata is written onto the subscription at each phase start;
                    // resending the userId keeps the webhook's primary user lookup intact.
                    Metadata = subscription.Metadata
                },
                new SubscriptionSchedulePhaseOptions
                {
                    Items = [new SubscriptionSchedulePhaseItemOptions { Price = priceId, Quantity = 1 }],
                    Duration = new SubscriptionSchedulePhaseDurationOptions { Interval = priceInterval, IntervalCount = 1 },
                    Metadata = subscription.Metadata
                }
            ]
        });

        return effectiveAt;
    }

    public async Task ReleaseScheduledChange(LiveSubscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return;
        }

        await new SubscriptionScheduleService(_client.Value).ReleaseAsync(subscription.ScheduleId);
    }

    private static LiveSubscription Project(Subscription subscription)
    {
        // Single-item subscriptions only: checkout creates one line per tier price.
        var item = subscription.Items?.Data?.FirstOrDefault();

        var live = new LiveSubscription
        {
            Id = subscription.Id,
            CustomerId = subscription.CustomerId,
            ItemId = item?.Id,
            PriceId = item?.Price?.Id,
            Status = subscription.Status,
            CurrentPeriodEnd = item?.CurrentPeriodEnd ?? default,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            Metadata = subscription.Metadata ?? [],
            ScheduleId = subscription.ScheduleId
        };

        var schedule = subscription.Schedule;
        if (schedule is not null && schedule.Status is "active" or "not_started")
        {
            var nextPhase = schedule.Phases?
                .Where(phase => phase.StartDate > DateTime.UtcNow)
                .OrderBy(phase => phase.StartDate)
                .FirstOrDefault();
            var nextPriceId = nextPhase?.Items?.FirstOrDefault()?.PriceId;

            if (nextPhase is not null && !string.IsNullOrEmpty(nextPriceId) && nextPriceId != live.PriceId)
            {
                live.ScheduledPriceId = nextPriceId;
                live.ScheduledStartsAt = nextPhase.StartDate;
            }
        }

        return live;
    }

    /// <summary>
    /// Carries a coupon/promotion applied to the current phase forward so scheduling a
    /// downgrade doesn't silently strip a discount the customer is still entitled to.
    /// </summary>
    private static List<SubscriptionSchedulePhaseDiscountOptions> MapDiscounts(SubscriptionSchedulePhase phase)
    {
        var discounts = phase.Discounts?
            .Where(discount => !string.IsNullOrEmpty(discount.CouponId) || !string.IsNullOrEmpty(discount.PromotionCodeId))
            .Select(discount => new SubscriptionSchedulePhaseDiscountOptions
            {
                Coupon = discount.CouponId,
                PromotionCode = discount.PromotionCodeId
            })
            .ToList();

        return discounts is { Count: > 0 } ? discounts : null;
    }
}
