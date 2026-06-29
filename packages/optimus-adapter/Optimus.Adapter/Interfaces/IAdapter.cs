using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Records;

namespace Optimus.Adapter.Interfaces;

public interface IAdapter
{
    public Task<BuyResult> Buy(StrategyDto strategy, string ticker);
    public Task<SellResult> Sell(TradeRecord position);
    public float GetPrice(string ticker);
}

/// <summary>
/// Result of a buy operation from an adapter.
/// Contains the actual entry position cost for state reconciliation.
/// </summary>
public class BuyResult
{
    public bool IsSuccess { get; private set; }
    public string FailureReason { get; private set; }

    /// <summary>
    /// The actual cost of the position (shares * price).
    /// Only set when IsSuccess is true.
    /// </summary>
    public decimal ActualEntryCost { get; private set; }

    /// <summary>
    /// The trade ID created by the adapter.
    /// Only set when IsSuccess is true.
    /// </summary>
    public string TradeId { get; private set; }

    private BuyResult() { }

    public static BuyResult Success(decimal actualEntryCost, string tradeId) => new()
    {
        IsSuccess = true,
        ActualEntryCost = actualEntryCost,
        TradeId = tradeId
    };

    public static BuyResult Failed(string reason) => new()
    {
        IsSuccess = false,
        FailureReason = reason
    };
}

/// <summary>
/// Result of a sell operation from an adapter.
/// Contains the actual close position value for state reconciliation.
/// </summary>
public class SellResult
{
    public bool IsSuccess { get; private set; }
    public string FailureReason { get; private set; }

    /// <summary>
    /// The actual value received from closing (shares * close price).
    /// Only set when IsSuccess is true.
    /// </summary>
    public decimal ActualCloseValue { get; private set; }

    /// <summary>
    /// The profit/loss from the trade.
    /// Only set when IsSuccess is true.
    /// </summary>
    public decimal Profit { get; private set; }

    private SellResult() { }

    public static SellResult Success(decimal actualCloseValue, decimal profit) => new()
    {
        IsSuccess = true,
        ActualCloseValue = actualCloseValue,
        Profit = profit
    };

    public static SellResult Failed(string reason) => new()
    {
        IsSuccess = false,
        FailureReason = reason
    };
}
