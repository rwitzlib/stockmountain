using System.Collections.Generic;
using System.Threading.Tasks;
using MarketViewer.Contracts.Requests.Market.Scan;
using Massive.Client.Responses;

namespace MarketViewer.Contracts.Interfaces;

public interface IAggregateCache
{
    Task<IEnumerable<MassiveAggregateResponse>> RetrieveAggregateResponses(ScanRequest request);
}