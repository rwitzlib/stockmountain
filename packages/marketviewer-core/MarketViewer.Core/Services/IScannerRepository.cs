using MarketViewer.Contracts.Dtos;

namespace MarketViewer.Core.Services;

public interface IScannerRepository
{
    Task<ScannerDto> Create(ScannerDto scanner);
    Task<ScannerDto> Get(string id);
    Task<IEnumerable<ScannerDto>> ListByUser(string userId);
    Task<ScannerDto> Update(ScannerDto scanner);
    Task<bool> Delete(string id);
}
