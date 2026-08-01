using Alpaca.Client.Interfaces;
using Alpaca.Client.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Alpaca.Client;

/// <summary>
/// Thin typed wrapper over Alpaca's trading API v2. Paper vs live is purely which named
/// HttpClient this instance was constructed with (base URL + key pair differ; the API
/// surface is identical). Rate limit is ~200 req/min per key — a non-issue at personal
/// scale, but batch before fan-out if this ever serves multiple tenants.
/// </summary>
public class AlpacaTradingClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AlpacaTradingClient> logger,
    string httpClientName) : IAlpacaTradingClient
{
    public const string HttpClientName = "alpaca-trading";
    public const string LiveHttpClientName = "alpaca-trading-live";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AlpacaClock> GetClock()
    {
        return await Get<AlpacaClock>("v2/clock");
    }

    public async Task<List<AlpacaCalendarDay>> GetCalendar(DateOnly start, DateOnly end)
    {
        return await Get<List<AlpacaCalendarDay>>($"v2/calendar?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}");
    }

    public async Task<AlpacaAccount> GetAccount()
    {
        return await Get<AlpacaAccount>("v2/account");
    }

    public async Task<List<AlpacaPosition>> GetPositions()
    {
        return await Get<List<AlpacaPosition>>("v2/positions");
    }

    public async Task<AlpacaOrder> GetOrder(string orderId)
    {
        return await Get<AlpacaOrder>($"v2/orders/{orderId}");
    }

    public async Task<AlpacaOrder> SubmitOrder(AlpacaOrderRequest request)
    {
        var client = httpClientFactory.CreateClient(httpClientName);
        var body = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("v2/orders", body);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // The response body carries the rejection reason (insufficient buying power,
            // duplicate client_order_id, invalid stop price, ...) — it is the whole story.
            logger.LogError("Failed to submit {Side} {Type} order for {Symbol}. Status: {StatusCode}, Body: {Body}",
                request.Side, request.Type, request.Symbol, response.StatusCode, json);
            return null;
        }

        return JsonSerializer.Deserialize<AlpacaOrder>(json, JsonOptions);
    }

    public async Task<CancelOrderResult> CancelOrder(string orderId)
    {
        var client = httpClientFactory.CreateClient(httpClientName);

        try
        {
            var response = await client.DeleteAsync($"v2/orders/{orderId}");

            if (response.IsSuccessStatusCode)
            {
                return CancelOrderResult.Canceled;
            }

            var body = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Cancel rejected for order {OrderId}. Status: {StatusCode}, Body: {Body}",
                orderId, response.StatusCode, body);

            return response.StatusCode switch
            {
                HttpStatusCode.UnprocessableEntity => CancelOrderResult.NotCancelable,
                HttpStatusCode.NotFound => CancelOrderResult.NotFound,
                _ => CancelOrderResult.Failed
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return CancelOrderResult.Failed;
        }
    }

    private async Task<T> Get<T>(string path) where T : class
    {
        var client = httpClientFactory.CreateClient(httpClientName);

        try
        {
            var response = await client.GetAsync(path);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Alpaca GET {Path} failed. Status: {StatusCode}", path, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alpaca GET {Path} threw", path);
            return null;
        }
    }
}
