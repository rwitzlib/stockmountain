using Microsoft.Extensions.Logging;
using SchwabApi.Interfaces;
using SchwabApi.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace SchwabApi.Clients;

public class AuthenticationClient(IHttpClientFactory _httpClientFactory, ILogger<AuthenticationClient> _logger) : IAuthenticationClient
{
    public string Code { get; set; }
    public Authentication Authentication { get; set; }

    public void Authenticate(string clientId, string redirectUrl)
    {
        using var client = _httpClientFactory.CreateClient("authentication");
        var url = $"{client.BaseAddress}/authorize?client_id={clientId}&redirect_uri={redirectUrl}";

        _logger.LogInformation("Click to authenticate: {url}\n", url);
        Console.WriteLine($"Click to authenticate: {url}\n");

        var returned_url = Console.ReadLine();

        Code = $"{returned_url.Split("code=")[1].Split("%")[0]}@";
    }

    public async Task<Authentication> GetTokenAsync(string clientId, string clientSecret)
    {
        var credentials = $"{clientId}:{clientSecret}";
        var bytes = Encoding.UTF8.GetBytes(credentials);
        var encodedCredentials = Convert.ToBase64String(bytes);

        using var client = _httpClientFactory.CreateClient("authentication");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {encodedCredentials}");

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", Code),
            new KeyValuePair<string, string>("redirect_uri", "https://127.0.0.1"),
        ]);
        var response = await client.PostAsync("token", content);

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var authentication = JsonSerializer.Deserialize<Authentication>(json);

        Authentication = authentication;

        return authentication;
    }
   
    public async Task<Authentication> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken)
    {
        var credentials = $"{clientId}:{clientSecret}";
        var bytes = Encoding.UTF8.GetBytes(credentials);
        var encodedCredentials = Convert.ToBase64String(bytes); 
        
        using var client = _httpClientFactory.CreateClient("authentication");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {encodedCredentials}");

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        ]);
        var response = await client.PostAsync("token", content);

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var authentication = JsonSerializer.Deserialize<Authentication>(json);

        Authentication = authentication;

        return authentication;
    }
}
