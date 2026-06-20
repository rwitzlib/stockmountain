namespace StockMountain.MarketData;

public readonly record struct BarSeriesKey(Symbol Symbol, Timeframe Timeframe, AdjustmentPolicy AdjustmentPolicy)
{
    public void Validate()
    {
        Timeframe.Validate();
    }
}
