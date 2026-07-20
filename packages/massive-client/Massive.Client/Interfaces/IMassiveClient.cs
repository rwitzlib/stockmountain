using Massive.Client.Requests;
using Massive.Client.Responses;

namespace Massive.Client.Interfaces;

public interface IMassiveClient
{
    Task<MassiveAggregateResponse> GetAggregates(MassiveAggregateRequest request);
    Task<MassiveTickerDetailsResponse> GetTickerDetails(string ticker, DateTime? date = null);
    Task<MassiveFloatResponse> GetFloats(string? ticker = null);
    Task<MassiveGetTickersResponse> GetTickers(MassiveGetTickersRequest request);
    Task<MassiveSnapshotResponse> GetAllTickersSnapshot(string tickers, bool includeOtc = false);
    Task<MassiveDailyMarketSummaryResponse> GetDailyMarketSummary(DateTime? date = null, bool includeOtc = false, bool adjusted = true);
}
