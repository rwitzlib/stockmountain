using MarketViewer.Contracts.Responses.Market;

namespace MarketDataAggregator;

public static class AggregateMerger
{
    /// <summary>
    /// Merges freshly fetched aggregates into a previously stored object so a run for an
    /// earlier date cannot truncate data already stored past its fetch window. Fresh bars
    /// win inside the fetched window; existing bars after the window and tickers absent
    /// from the fresh fetch are preserved.
    /// </summary>
    public static List<StocksResponse> Merge(List<StocksResponse> fresh, List<StocksResponse> existing, long fetchWindowEnd)
    {
        var merged = new Dictionary<string, StocksResponse>();

        foreach (var response in fresh)
        {
            if (response?.Ticker is not null)
            {
                merged.TryAdd(response.Ticker, response);
            }
        }

        foreach (var existingResponse in existing)
        {
            if (existingResponse?.Ticker is null)
            {
                continue;
            }

            if (!merged.TryGetValue(existingResponse.Ticker, out var freshResponse))
            {
                merged.Add(existingResponse.Ticker, existingResponse);
                continue;
            }

            if (existingResponse.Results is null)
            {
                continue;
            }

            // Fresh bars all fall within the fetch window, so appending the existing
            // bars past the window keeps the list sorted by timestamp.
            var laterBars = existingResponse.Results.Where(bar => bar.Timestamp > fetchWindowEnd);

            freshResponse.Results ??= [];
            freshResponse.Results.AddRange(laterBars);
        }

        return merged.Values.ToList();
    }
}
