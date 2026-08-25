namespace MarketViewer.Contracts.Records;

/// <summary>
/// One append-only row in the billing ledger: a money or credit event sourced from a Stripe
/// webhook (or the monthly refill Lambda). Stripe remains the system of record for money;
/// this ledger is the LTV source and the audit trail for credit grants.
/// </summary>
public class BillingLedgerRecord
{
    public string UserId { get; set; }

    /// <summary>
    /// Sort key: "{ISO-8601 UTC timestamp}#{stripe event id}". Because the Stripe event id is
    /// part of the key, webhook redelivery collides on a conditional put and becomes a no-op.
    /// </summary>
    public string EventKey { get; set; }

    /// <summary>One of <see cref="BillingLedgerEntryType"/>.</summary>
    public string Type { get; set; }

    /// <summary>Money moved, in cents. Zero for non-money grants; negative for refunds.</summary>
    public long AmountCents { get; set; }

    /// <summary>Credits granted (positive) or clawed back (negative) by this event.</summary>
    public float Credits { get; set; }

    public string StripeEventId { get; set; }
    public string StripeInvoiceId { get; set; }
    public string StripePaymentIntentId { get; set; }
    public string Tier { get; set; }
    public string Description { get; set; }

    /// <summary>
    /// <see cref="BillingLedgerStatus.Pending"/> while the credit mutation this row guards has
    /// not been applied yet; absent (null) once applied. Rows written without a status are
    /// applied. Lets a re-run resume a row whose mutation was interrupted instead of treating
    /// the collision as already-applied.
    /// </summary>
    public string Status { get; set; }
}

public static class BillingLedgerStatus
{
    public const string Pending = "pending";
}

public static class BillingLedgerEntryType
{
    public const string SubscriptionPayment = "subscription_payment";
    public const string TopupPurchase = "topup_purchase";
    public const string Refund = "refund";
    public const string MonthlyRefill = "monthly_refill";
    public const string SignupGrant = "signup_grant";
    public const string UpgradeGrant = "upgrade_grant";
}
