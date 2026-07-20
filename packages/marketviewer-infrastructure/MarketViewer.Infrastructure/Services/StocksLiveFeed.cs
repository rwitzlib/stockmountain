using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Massive.Client.Responses;
using Massive.Client.Requests;
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
                logger.LogInformation("Connecting to MassiveApi WebSocket at: {time}", DateTimeOffset.Now);

                var messageBuilder = new StringBuilder();

                await socket.ConnectAsync(new Uri("wss://socket.massive.com/stocks"), cancellationToken);

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
                        logger.LogInformation("Disconnected from MassiveApi WebSocket.");
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
                            logger.LogInformation("Connected to MassiveApi WebSocket successfully.");
                        }
                    }

                    if (_isConnected && !_isAuthenticated)
                    {
                        if (firstResponse.Status is "auth_success")
                        {
                            _isAuthenticated = true;
                            logger.LogInformation("Authenticated to MassiveApi WebSocket successfully.");
                        }
                        else
                        {
                            var request = JsonSerializer.Serialize(new MassiveWebsocketRequest
                            {
                                Action = "auth",
                                Params = Environment.GetEnvironmentVariable("MASSIVE_TOKEN")
                                    ?? configuration.GetSection("Tokens").GetValue<string>("MassiveApi")
                            });

                            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);
                        }
                    }

                    if (_isConnected && _isAuthenticated && !_isSubscribed)
                    {
                        if (firstResponse.Status is "success")
                        {
                            _isSubscribed = true;
                            logger.LogInformation("Subscribed to MassiveApi WebSocket successfully.");
                        }
                        else
                        {
                            var request = JsonSerializer.Serialize(new MassiveWebsocketRequest
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
            logger.LogError(e, "Error in MassiveApi WebSocket connection: {message}. ", e.Message);
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
            logger.LogInformation("MassiveApi WebSocket service stopped.");
        }
    }

    private bool TryDeserialize(string json, out List<MassiveWebsocketAggregateResponse> response)
    {
        try
        {
            response = JsonSerializer.Deserialize<List<MassiveWebsocketAggregateResponse>>(json);

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
