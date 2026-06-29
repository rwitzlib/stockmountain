using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class Balance
{
    public float AccountValue { get; set; }
    public float AccruedInterest { get; set; }
    public float AvailableFunds { get; set; }
    public float AvailableFundsNonMarginableTrade { get; set; }
    public float BondValue { get; set; }
    public float BuyingPower { get; set; }
    public float BuyingPowerNonMarginableTrade { get; set; }
    public float CashAvailableForTrading { get; set; }
    public float CashBalance { get; set; }
    public float CashReceipts { get; set; }
    public float DayTradingBuyPower { get; set; }
    public float DayTradingBuyingPower { get; set; }
    public float DayTradingBuyingPowerCall { get; set; }
    public float DayTradingEquityCall { get; set; }
    public float DayTradingPowerCall { get; set; }
    public float Equity { get; set; }
    public float EquityPercentage { get; set; }
    public bool IsInCall { get; set; }
    public float LiquidationValue { get; set; }
    public float LongMarginValue { get; set; }
    public float LongOptionMarketValue { get; set; }
    public float LongStockValue { get; set; }
    public float MaintenanceCall { get; set; }
    public float MaintenanceRequirement { get; set; }
    public float Margin { get; set; }
    public float MarginBalance { get; set; }
    public float MarginEquity { get; set; }
    public float MoneyMarketFund { get; set; }
    public float MutualFundValue { get; set; }
    public float OptionBuyingPower { get; set; }
    public float PendingDeposits { get; set; }
    public float RegTCall { get; set; }
    public float ShortBalance { get; set; }
    public float ShortMarginValue { get; set; }
    public float ShortOptionMarketValue { get; set; }
    public float ShortStockValue { get; set; }
    public float Sma { get; set; }
    public float StockBuyingPower { get; set; }
    public float TotalCash { get; set; }
    public float UnsettledCash { get; set; }
}