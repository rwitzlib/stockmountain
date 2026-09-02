namespace MarketViewer.Contracts.Responses.Billing;

public class PlanChangeResponse
{
    /// <summary>
    /// "applied" — the subscription is on the new price and the webhook is in flight;
    /// "scheduled" — the change takes effect at <see cref="EffectiveAt"/>;
    /// "requires_action" — the prorated payment needs the customer's input (3-D Secure, a
    /// declined card): the subscription is unchanged until the invoice at
    /// <see cref="PaymentUrl"/> is paid, after which Stripe applies the pending update.
    /// </summary>
    public string Status { get; set; }

    public DateTime? EffectiveAt { get; set; }

    /// <summary>Stripe-hosted invoice page for completing an incomplete prorated payment.</summary>
    public string PaymentUrl { get; set; }
}

public static class PlanChangeStatus
{
    public const string Applied = "applied";
    public const string Scheduled = "scheduled";
    public const string RequiresAction = "requires_action";
}
