using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Massive.Client.Responses;
using Massive.Client.Requests;
using MarketViewer.Contracts.Caching;
using MarketViewer.Infrastructure.Config;

namespace MarketViewer.Infrastructure.Services;

public class StocksLiveFeed(
    IConfiguration configuration,
    MarketDataConfig marketDataConfig,
    IMarketCache marketCache,
    ILogger<StocksLiveFeed> logger) : BackgroundService
{
    // Massive serves delayed and real-time data from different clusters; a token
    // entitled only to the delayed plan authenticates on the real-time cluster but
    // streams nothing. Keyed off DelayMinutes so the socket host and the scan data
    // clock can never disagree about which plan is active.
    private const string RealTimeUrl = "wss://socket.massive.com/stocks";
    private const string DelayedUrl = "wss://delayed.massive.com/stocks";

    private string SocketUrl => marketDataConfig.DelayMinutes > 0 ? DelayedUrl : RealTimeUrl;

    private const int ReceiveBufferSize = 1024 * 64;

    // A dead TCP peer can leave ReceiveAsync hanging forever with no exception;
    // Massive sends status traffic even outside market hours, so a long silence
    // means the connection is gone. Timing out forces a reconnect.
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    // Guard against a runaway fragmented message exhausting memory. Never used to
    // drop data silently — hitting this logs an error before clearing.
    private const int MaxMessageChars = 8 * 1024 * 1024;

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is "local")
        {
            return;
        }

        var backoff = InitialBackoff;
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;

            try
            {
                // ClientWebSocket cannot be reused after a close/abort; one per attempt.
                using var socket = new ClientWebSocket();

                logger.LogInformation("Connecting to MassiveApi WebSocket at {url} (attempt {attempt}) at: {time}", SocketUrl, attempt, DateTimeOffset.Now);

                await socket.ConnectAsync(new Uri(SocketUrl), cancellationToken);

                var subscribed = await RunSession(socket, cancellationToken);

                if (subscribed)
                {
                    backoff = InitialBackoff;
                    attempt = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "MassiveApi WebSocket session failed (attempt {attempt}): {message}", attempt, e.Message);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            logger.LogInformation("Reconnecting to MassiveApi WebSocket in {backoff}s.", backoff.TotalSeconds);

            try
            {
                await Task.Delay(backoff, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            backoff = backoff * 2 > MaxBackoff ? MaxBackoff : backoff * 2;
        }

        logger.LogInformation("MassiveApi WebSocket service stopped.");
    }

    /// <summary>
    /// Runs one connection's handshake + receive loop until the socket closes, times
    /// out, or errors. Returns whether the session ever reached the subscribed state
    /// (used to reset the reconnect backoff).
    /// </summary>
    private async Task<bool> RunSession(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var isConnected = false;
        var isAuthenticated = false;
        var isSubscribed = false;

        var messageBuilder = new StringBuilder();
        var buffer = new byte[ReceiveBufferSize];
        var lastMessageAt = DateTimeOffset.UtcNow;

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;

            using (var receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                receiveTimeout.CancelAfter(ReceiveTimeout);
                try
                {
                    result = await socket.ReceiveAsync(buffer, receiveTimeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "MassiveApi WebSocket stale: no message for {seconds}s (last at {lastMessageAt}); forcing reconnect.",
                        ReceiveTimeout.TotalSeconds, lastMessageAt);
                    socket.Abort();
                    return isSubscribed;
                }
            }

            lastMessageAt = DateTimeOffset.UtcNow;

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.LogWarning("MassiveApi WebSocket closed by server: {status} {description}; will reconnect.",
                    result.CloseStatus, result.CloseStatusDescription);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                return isSubscribed;
            }

            var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
            messageBuilder.Append(chunk);

            if (!result.EndOfMessage)
            {
                if (messageBuilder.Length > MaxMessageChars)
                {
                    logger.LogError("MassiveApi WebSocket message exceeded {max} chars; discarding fragment.", MaxMessageChars);
                    messageBuilder.Clear();
                }
                continue;
            }

            string completeMessage = messageBuilder.ToString();
            messageBuilder.Clear();

            if (!TryDeserialize(completeMessage, out var response))
            {
                continue;
            }

            if (isConnected && isAuthenticated && isSubscribed)
            {
                var aggregateResponses = response.Where(item => item.Event is "A");

                foreach (var aggregateResponse in aggregateResponses)
                {
                    marketCache.AddLiveBar(aggregateResponse);
                }
                continue;
            }

            var firstResponse = response.First();

            if (!isConnected)
            {
                if (firstResponse.Status is "connected")
                {
                    isConnected = true;
                    logger.LogInformation("Connected to MassiveApi WebSocket successfully.");
                }
            }

            if (isConnected && !isAuthenticated)
            {
                if (firstResponse.Status is "auth_success")
                {
                    isAuthenticated = true;
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

            if (isConnected && isAuthenticated && !isSubscribed)
            {
                if (firstResponse.Status is "success")
                {
                    isSubscribed = true;
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

        logger.LogWarning("MassiveApi WebSocket receive loop ended (state: {state}); will reconnect.", socket.State);
        return isSubscribed;
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
