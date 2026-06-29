using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Records;
using Optimus.Adapter.Interfaces;

namespace Optimus.Adapter;

public class SchwabAdapter : IAdapter
{
    public Task<BuyResult> Buy(StrategyDto strategy, string ticker)
    {
        throw new NotImplementedException();
    }

    public Task<SellResult> Sell(TradeRecord position)
    {
        throw new NotImplementedException();
    }

    public float GetPrice(string ticker)
    {
        throw new NotImplementedException();
    }
}
