using MarketViewer.Contracts.Dtos;

namespace MarketViewer.Core.Services;

public interface IScannerRepository
{
    Task<ScannerDto> Create(ScannerDto scanner, CancellationToken cancellationToken = default);
    Task<ScannerDto> Get(string id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ScannerDto>> ListByUser(string userId, CancellationToken cancellationToken = default);
    Task<ScannerDto> Update(ScannerDto scanner, CancellationToken cancellationToken = default);
    Task<bool> Delete(string id, CancellationToken cancellationToken = default);
}
