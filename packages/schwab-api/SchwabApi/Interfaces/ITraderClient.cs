using SchwabApi.Models;
using SchwabApi.Requests;

namespace SchwabApi.Interfaces;

public interface ITraderClient
{
    /// <summary>
    /// Get list of account numbers and their encrypted values.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<SecuritiesAccount>> GetAccountNumbersAsync();

    /// <summary>
    /// Get linked account(s) balances and position for the logged in user.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Account>> GetAccountsAsync(bool showPositions = false);

    /// <summary>
    /// Get a specific account balance and positions for the logged in user.
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <returns></returns>
    Task<Account> GetAccountAsync(string accountNumber, bool showPositions = false);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<IEnumerable<Order>> GetOrders(GetOrdersRequest request);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<IEnumerable<Order>> GetOrdersForAccount(string accountNumber, GetOrdersRequest request);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <param name="orderNumber"></param>
    /// <returns></returns>
    Task<Order> GetOrderForAccount(string accountNumber, string orderNumber);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <param name="orderNumber"></param>
    /// <returns></returns>
    Task<bool> CancelOrder(string accountNumber, string orderNumber);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    Task<bool> CreateOrder(string accountId, Order order);
}
