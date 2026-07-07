using FluentValidation;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyUpdateHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ScannerCache scannerCache,
    IValidator<StrategyUpdateRequest> validator,
    ILogger<StrategyUpdateHandler> logger)
{
    public async Task<OperationResult<StrategyResponse>> Handle(StrategyUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Validation failed for strategy update request: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
                };
            }

            var existingStrategy = await strategyRepository.Get(request.Id);

            if (existingStrategy == null || existingStrategy.UserId != authContext.UserId)
            {
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            var updatedstrategy = new StrategyDto
            {
                Id = request.Id,
                UserId = authContext.UserId,
                Name = request.Name,
                State = request.State,
                Visibility = request.Visibility,
                Type = request.Type,
                Integration = request.Integration,
                PositionSettings = request.PositionSettings,
                EntrySettings = request.EntrySettings,
                ExitSettings = request.ExitSettings
            };

            var strategy = await strategyRepository.Update(updatedstrategy, existingStrategy);

            if (strategy == null)
            {
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to update strategy."]
                };
            }

            var response = new StrategyResponse
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
            };

            var strategyHash = response.EntrySettings.ComputeStrategyHash();
            scannerCache.TryAddStrategy(strategyHash, response.EntrySettings);

            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.OK,
                Data = response,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update strategy for user {UserId}", authContext.UserId);
            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Failed to update strategy."]
            };
        }
    }
}
