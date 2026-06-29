using MarketViewer.Contracts.Records.Scan;

namespace MarketViewer.Core.Services;

public interface IScanRepository
{
    Task<ScanRecord> Create(ScanRecord scan);

    Task<ScanRecord> Get(string strategyHash, long window);
}