using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Studies;

public interface IStudy
{
    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse);
}
