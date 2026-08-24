using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Requests.Billing;

public class CheckoutSessionRequest
{
    public CheckoutKind Kind { get; set; }

    /// <summary>Subscription tier ("Pro", "Premium") or credit pack id ("PackSmall", "PackLarge").</summary>
    public string Id { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<CheckoutKind>))]
public enum CheckoutKind
{
    Subscription = 1,
    Pack = 2
}
