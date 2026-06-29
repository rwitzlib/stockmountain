using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Polygon.Client.Responses;
using Polygon.Client.Requests;
using MarketViewer.Contracts.Caching;

namespace MarketViewer.Infrastructure.Services;

public class StocksLiveFeed(
    IConfiguration configuration,
    IMarketCache marketCache,
    ILogger<StocksLiveFeed> logger) : BackgroundService
{
    private bool _isConnected = false;
    private bool _isAuthenticated = false;
    private bool _isSubscribed = false;

    private const int MaxBufferSize = 1024 * 64;

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is "local")
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Connecting to PolygonApi Websocket at: {time}", DateTimeOffset.Now);

                var messageBuilder = new StringBuilder();

                await socket.ConnectAsync(new Uri("wss://socket.polygon.io/stocks"), cancellationToken);

                var buffer = new byte[MaxBufferSize];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken);

                    var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);

                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    string completeMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger.LogInformation("Disconnected from PolygonApi Websocket.");
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                        return;
                    }

                    if (result.Count >= MaxBufferSize)
                    {
                        logger.LogError("Total bytes received: {count}", result.Count);
                        continue;
                    }

                    if (!TryDeserialize(completeMessage, out var response))
                    {
                        continue;
                    }

                    if (_isConnected && _isAuthenticated && _isSubscribed)
                    {
                        var aggregateResponses = response.Where(item => item.Event is "A");

                        foreach (var aggregateResponse in aggregateResponses)
                        {
                            marketCache.AddLiveBar(aggregateResponse);
                        }
                        continue;
                    }

                    var firstResponse = response.First();

                    if (!_isConnected)
                    {
                        if (firstResponse.Status is "connected")
                        {
                            _isConnected = true;
                            logger.LogInformation("Connected to PolygonApi Websocket successfully.");
                        }
                    }

                    if (_isConnected && !_isAuthenticated)
                    {
                        if (firstResponse.Status is "auth_success")
                        {
                            _isAuthenticated = true;
                            logger.LogInformation("Authenticated to PolygonApi Websocket successfully.");
                        }
                        else
                        {
                            var request = JsonSerializer.Serialize(new PolygonWebsocketRequest
                            {
                                Action = "auth",
                                Params = configuration.GetSection("Tokens").GetValue<string>("PolygonApi")
                            });

                            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);
                        }
                    }

                    if (_isConnected && _isAuthenticated && !_isSubscribed)
                    {
                        if (firstResponse.Status is "success")
                        {
                            _isSubscribed = true;
                            logger.LogInformation("Subscribed to PolygonApi Websocket successfully.");
                        }
                        else
                        {
                            var request = JsonSerializer.Serialize(new PolygonWebsocketRequest
                            {
                                Action = "subscribe",
                                Params = "A.*"
                            });

                            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in PolygonApi Websocket connection: {message}. ", e.Message);
            logger.LogError("WebSocket state: {state}", socket.State);
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopped", CancellationToken.None);
            }
        }
        finally
        {
            _isConnected = false;
            _isAuthenticated = false;
            _isSubscribed = false;
            logger.LogInformation("PolygonApi Websocket service stopped.");
        }
    }

    private bool TryDeserialize(string json, out List<PolygonWebsocketAggregateResponse> response)
    {
        try
        {
            response = JsonSerializer.Deserialize<List<PolygonWebsocketAggregateResponse>>(json);

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing WebSocket message: {message}. ", e.Message);
            logger.LogError("WebSocket message: {message}", json);
            response = null;
            return false;
        }
    }
}
