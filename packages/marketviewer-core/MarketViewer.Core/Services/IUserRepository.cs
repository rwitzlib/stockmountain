using MarketViewer.Contracts.Records;

namespace MarketViewer.Core.Services;

public interface IUserRepository
{
    Task<bool> Put(UserRecord user);
    Task<UserRecord> Get(string id);
}