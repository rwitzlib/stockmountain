using SchwabApi.Converters;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class Order
{
    public string Session { get; set; }
    public string Duration { get; set; }
    public string OrderType { get; set; }

    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset CancelTime { get; set; }
    public string ComplexOrderStrategyType { get; set; }
    public float Quantity { get; set; }
    public float FilledQuantity { get; set; }
    public float RemainingQuantity { get; set; }
    public string RequestedDestination { get; set; }
    public string DestinationLinkName { get; set; }

    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset ReleaseTime { get; set; }
    public float StopPrice { get; set; }
    public string StopPriceLinkBasis { get; set; }
    public string StopPriceLinkType { get; set; }
    public float StopPriceOffset { get; set; }
    public string StopType { get; set; }
    public string PriceLinkBasis { get; set; }
    public string PriceLinkType { get; set; }
    public float Price { get; set; }
    public string TaxLotMethod { get; set; }
    public List<OrderLeg> OrderLegCollection { get; set; }
    public float ActivationPrice { get; set; }
    public string SpecialInstruction { get; set; }
    public string OrderStrategyType { get; set; }
    public long OrderId { get; set; }
    public bool Cancelable { get; set; }
    public bool Editable { get; set; }
    public string Status { get; set; }

    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset EnteredTime { get; set; }

    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset CloseTime { get; set; }
    public string Tag { get; set; }
    public int AccountNumber { get; set; }
    public List<OrderActivity> OrderActivityCollection { get; set; }
    public List<string> ReplacingOrderCollection { get; set; }
    public List<string> ChildOrderStrategies { get; set; }
    public string StatusDescription { get; set; }
}
