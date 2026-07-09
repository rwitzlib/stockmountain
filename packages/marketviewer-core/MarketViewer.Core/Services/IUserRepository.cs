using MarketViewer.Contracts.Records;

namespace MarketViewer.Core.Services;

public interface IUserRepository
{
    Task<bool> Put(UserRecord user);
    Task<bool> Provision(UserRecord user);
    Task<UserRecord> Get(string id);
    Task<bool> TryDebitCredits(string id, float credits);
}