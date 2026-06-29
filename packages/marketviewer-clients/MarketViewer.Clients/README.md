# MarketViewer.Clients

This project provides HTTP client classes for interacting with the MarketViewer API endpoints. It includes clients for Strategy, Trade, and User management operations.

## Features

- **StrategyClient**: Manage trading strategies (create, read, update, delete, list)
- **TradeClient**: Manage trades (open, close, list)
- **UserClient**: Manage users (get, list)
- **Base Authentication**: JWT token support for authenticated endpoints
- **Logging**: Built-in logging support for all HTTP operations
- **Error Handling**: Graceful error handling with proper status code responses

## Usage

### Setup

First, register the clients in your dependency injection container:

```csharp
services.AddHttpClient<IStrategyClient, StrategyClient>(client =>
{
    client.BaseAddress = new Uri("https://your-api-base-url/");
});

services.AddHttpClient<ITradeClient, TradeClient>(client =>
{
    client.BaseAddress = new Uri("https://your-api-base-url/");
});

services.AddHttpClient<IUserClient, UserClient>(client =>
{
    client.BaseAddress = new Uri("https://your-api-base-url/");
});
```

### Authentication

Set the JWT token for authenticated endpoints:

```csharp
var strategyClient = serviceProvider.GetService<IStrategyClient>();
strategyClient.SetAuthorizationToken("your-jwt-token");
```

### Strategy Operations

```csharp
// Create a strategy
var createRequest = new StrategyPutRequest
{
    Name = "My Strategy",
    Enabled = true,
    Public = false,
    // ... other properties
};
var strategy = await strategyClient.CreateAsync(createRequest);

// Get a strategy
var strategy = await strategyClient.GetAsync("strategy-id");

// List strategies
var listRequest = new StrategyListRequest();
var strategies = await strategyClient.ListAsync(listRequest);

// Update a strategy
var updatedStrategy = await strategyClient.UpdateAsync("strategy-id", updateRequest);

// Delete a strategy
var success = await strategyClient.DeleteAsync("strategy-id");
```

### Trade Operations

```csharp
// Open a trade
var openRequest = new TradeOpenRequest
{
    StrategyId = "strategy-id",
    Ticker = "AAPL",
    Shares = 100,
    // ... other properties
};
var success = await tradeClient.OpenAsync(openRequest);

// List trades
var listRequest = new TradeListRequest
{
    User = "user-id",
    Strategy = "strategy-id"
};
var trades = await tradeClient.ListAsync(listRequest);

// Close a trade
var closeRequest = new TradeCloseRequest
{
    ClosePrice = 150.00f,
    // ... other properties
};
var success = await tradeClient.CloseAsync("trade-id", closeRequest);
```

### User Operations

```csharp
// Get a user
var user = await userClient.GetAsync("user-id");

// List all users
var users = await userClient.ListAsync();
```

## Dependencies

- Microsoft.Extensions.Http
- Microsoft.Extensions.Logging.Abstractions
- System.Text.Json
- MarketViewer.Contracts
- MarketViewer.Core

## Error Handling

All client methods handle errors gracefully:
- Network errors return `null` or `false` depending on the method
- HTTP errors are logged with appropriate log levels
- Exceptions are caught and logged, returning default values

## Logging

The clients use `ILogger` for logging HTTP operations:
- Debug level: Request details
- Warning level: HTTP error responses
- Error level: Exceptions and network errors 