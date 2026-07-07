using FluentValidation;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyCreateHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ScannerCache scannerCache,
    IValidator<StrategyCreateRequest> validator,
    ILogger<StrategyCreateHandler> logger)
{
    public async Task<OperationResult<StrategyResponse>> Handle(StrategyCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Validation failed for strategy create request: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
                };
            }

            logger.LogInformation("Creating strategy '{Name}' for user {UserId}", request.Name, authContext.UserId);

            var strategyId = Guid.NewGuid().ToString("N");
            var strategy = new StrategyDto
            {
                Id = strategyId,
                UserId = authContext.UserId.ToString(),
                Name = request.Name,
                State = request.State,
                Visibility = request.Visibility,
                Type = request.Type,
                Integration = request.Integration,
                PositionSettings = request.PositionSettings,
                ExitSettings = request.ExitSettings,
                EntrySettings = request.EntrySettings
            };

            var strategyDto = await strategyRepository.Create(strategy);

            if (strategyDto == null)
            {
                logger.LogError("Repository failed to create strategy '{Name}' for user {UserId}", request.Name, authContext.UserId);
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to create strategy."]
                };
            }

            var response = new StrategyResponse
            {
                Id = strategyDto.Id,
                Name = strategyDto.Name,
                Integration = strategyDto.Integration,
                Type = strategyDto.Type,
                Visibility = strategyDto.Visibility,
                State = strategyDto.State,
                PositionSettings = strategyDto.PositionSettings,
                ExitSettings = strategyDto.ExitSettings,
                EntrySettings = strategyDto.EntrySettings,
            };

            var strategyHash = response.EntrySettings.ComputeStrategyHash();
            scannerCache.TryAddStrategy(strategyHash, response.EntrySettings);

            logger.LogInformation("Successfully created strategy '{Name}' with ID {StrategyId} for user {UserId}",
                request.Name, strategyId, authContext.UserId);

            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.OK,
                Data = response,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating strategy '{Name}' for user {UserId}", request.Name, authContext.UserId);
            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while creating the strategy."]
            };
        }
    }
}
