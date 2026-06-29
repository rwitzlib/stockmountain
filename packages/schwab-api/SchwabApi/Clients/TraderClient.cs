using SchwabApi.Interfaces;
using SchwabApi.Models;
using SchwabApi.Requests;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchwabApi.Clients;

public class TraderClient(IHttpClientFactory _httpClientFactory, IAuthenticationClient _authenticationClient) : ITraderClient
{
    private readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IEnumerable<SecuritiesAccount>> GetAccountNumbersAsync()
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var response = await client.GetAsync("accounts/accountNumbers");

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var accounts = JsonSerializer.Deserialize<IEnumerable<SecuritiesAccount>>(json, Options);

        return accounts;
    }

    public async Task<IEnumerable<Account>> GetAccountsAsync(bool showPositions = false)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var url = showPositions ? "accounts?fields=positions" : "accounts";
        var response = await client.GetAsync(url);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var accounts = JsonSerializer.Deserialize<IEnumerable<Account>>(json, Options);

        return accounts;
    }
    
    public async Task<Account> GetAccountAsync(string hashValue, bool showPositions = false)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var url = showPositions ? $"accounts/{hashValue}?fields=positions" : $"accounts/{hashValue}";
        var response = await client.GetAsync(url);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var account = JsonSerializer.Deserialize<Account>(json, Options);

        return account;
    }

    public async Task<IEnumerable<Order>> GetOrdersForAccount(string accountNumber, GetOrdersRequest request)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var query = "";
        if (request.MaxResults is not null)
        {
            query += $"maxResults={request.MaxResults}&";
        }
        query += $"fromEnteredTime={request.From:yyyy-MM-ddThh:mm:ss.fffZ}&";
        query += $"toEnteredTime={request.To:yyyy-MM-ddThh:mm:ss.fffZ}&";
        if (request.Status is not null)
        {
            query += $"status={request.Status}";
        }
        query = query.TrimEnd('&');

        var response = await client.GetAsync($"accounts/{accountNumber}/orders?{query}");

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }


        var orders = JsonSerializer.Deserialize<IEnumerable<Order>>(json, Options);

        return orders;
    }

    public async Task<Order> GetOrderForAccount(string accountNumber, string orderNumber)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var response = await client.GetAsync($"accounts/{accountNumber}/orders/{orderNumber}");

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var order = JsonSerializer.Deserialize<Order>(json, Options);

        return order;
    }
    
    public async Task<IEnumerable<Order>> GetOrders(GetOrdersRequest request)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var query = "";
        if (request.MaxResults is not null)
        {
            query += $"maxResults={request.MaxResults}&";
        }
        query += $"fromEnteredTime={request.From:yyyy-MM-ddThh:mm:ss.fffZ}&";
        query += $"toEnteredTime={request.To:yyyy-MM-ddThh:mm:ss.fffZ}&";
        if (request.Status is not null)
        {
            query += $"status={request.Status}";
        }
        query = query.TrimEnd('&');

        var response = await client.GetAsync($"orders?{query}");

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var orders = JsonSerializer.Deserialize<IEnumerable<Order>>(json, Options);

        return orders;
    }

    public async Task<bool> CancelOrder(string accountId, string orderId)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");

        var response = await client.DeleteAsync($"accounts/{accountId}/orders{orderId}");

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> CreateOrder(string accountId, Order order)
    {
        using var client = _httpClientFactory.CreateClient("trader");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {_authenticationClient.Authentication.AccessToken}");
            
        var json = JsonSerializer.Serialize(order, Options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"accounts/{accountId}/orders", content);

        var responseJson = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        return true;
    }
}
