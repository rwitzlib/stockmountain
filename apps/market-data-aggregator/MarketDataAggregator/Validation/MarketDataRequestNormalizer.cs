using MarketViewer.Contracts.Enums;

namespace MarketDataAggregator.Validation;

public static class MarketDataRequestNormalizer
{
    public static bool TryPrepareAggregatorRequest(MarketDataAggregatorRequest request)
    {
        if (request.Type is not null &&
            !request.Type.Equals("auto", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        if (request.Type?.Equals("auto", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            request.Date = DateTimeOffset.Now.Date.AddDays(-1);
            request.Source = "schedule";
        }

        return true;
    }

    public static void NormalizeOrchestratorRequest(MarketDataOrchestratorRequest request)
    {
        if (request.MaxConcurrency <= 0)
        {
            request.MaxConcurrency = 1;
        }

        request.Timespans = request.Timespans
            .Where(timespan => timespan is Timespan.minute or Timespan.hour or Timespan.day)
            .Distinct()
            .ToList();
    }
}
