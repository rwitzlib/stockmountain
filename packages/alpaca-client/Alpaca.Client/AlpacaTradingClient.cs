using Alpaca.Client.Interfaces;
using Alpaca.Client.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Alpaca.Client;

public class AlpacaTradingClient(IHttpClientFactory httpClientFactory, ILogger<AlpacaTradingClient> logger) : IAlpacaTradingClient
{
    public const string HttpClientName = "alpaca-trading";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AlpacaClock> GetClock()
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync("v2/clock");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get clock from Alpaca. Status: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AlpacaClock>(json, JsonOptions);
    }

    public async Task<List<AlpacaCalendarDay>> GetCalendar(DateOnly start, DateOnly end)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"v2/calendar?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get calendar from Alpaca. Status: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<AlpacaCalendarDay>>(json, JsonOptions);
    }
}
