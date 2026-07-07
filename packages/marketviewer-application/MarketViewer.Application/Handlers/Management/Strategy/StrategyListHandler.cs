using Amazon.Runtime;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyListHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ILogger<StrategyCreateHandler> logger)
{
    public async Task<OperationResult<IEnumerable<StrategyResponse>>> Handle(StrategyListRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!authContext.IsAuthenticated && request.Visibility != VisibilityType.Public)
            {
                logger.LogWarning("Unauthenticated access attempt to list non-public strategies.");
                return new OperationResult<IEnumerable<StrategyResponse>>
                {
                    Status = HttpStatusCode.OK,
                    Data = []
                };
            }

            var strategies = request.Visibility.HasValue ? await strategyRepository.ListByVisibility(request.Visibility.Value) : await strategyRepository.ListByUser(authContext.UserId);

            logger.LogInformation("Retrieved {Count} strategies for user {UserId} (Visibility: {Visibility})",
                strategies.Count(), authContext.UserId, request.Visibility);

            var response = new List<StrategyResponse>();
            foreach (var strategy in strategies)
            {
                response.Add(new StrategyResponse
                {
                    Id = strategy.Id,
                    Name = strategy.Name,
                    Integration = strategy.Integration,
                    Type = strategy.Type,
                    State = strategy.State,
                    Visibility = strategy.Visibility,
                    PositionSettings = strategy.PositionSettings,
                    ExitSettings = strategy.ExitSettings,
                    EntrySettings = strategy.EntrySettings
                });
            }

            return new OperationResult<IEnumerable<StrategyResponse>>
            {
                Status = HttpStatusCode.OK,
                Data = response,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list strategies for user {UserId}", authContext.UserId);
            return new OperationResult<IEnumerable<StrategyResponse>>
            {
                Status = HttpStatusCode.OK,
                Data = []
            };
        }
    }
}
