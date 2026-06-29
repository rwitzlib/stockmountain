using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class Position
{
    public float ShortQuantity { get; set; }
    public float AveragePrice { get; set; }
    public float CurrentDayProfitLoss { get; set; }
    public float CurrentDayProfitLossPercentage { get; set; }
    public float LongQuantity { get; set; }
    public float SettledLongQuantity { get; set; }
    public float SettledShortQuantity { get; set; }
    public float AgedQuantity { get; set; }
    public Instrument Instrument { get; set; }
    public float MarketValue { get; set; }
    public float MaintenenanceRequirement { get; set; }
    public float AverageLongPrice { get; set; }
    public float AverageShortPrice { get; set; }
    public float TaxLogAverageLongPrice { get; set; }
    public float TaxLogAverageShortPrice { get; set; }
    public float LongOpenProfitLoss { get; set; }
    public float ShortOpenProfitLoss { get; set; }
    public float PreviousSessionLongQuantity { get; set; }
    public float PreviousSessionShortQuantity { get; set; }
    public float CurrentDayCost { get; set; }
}
