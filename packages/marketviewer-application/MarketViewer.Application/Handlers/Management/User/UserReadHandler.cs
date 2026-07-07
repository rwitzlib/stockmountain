using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.User;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.User;

public class UserReadHandler(
    AuthContext authContext,
    IUserRepository repository,
    IHttpContextAccessor contextAccessor,
    ILogger<UserReadHandler> logger)
{
    public async Task<OperationResult<UserResponse>> Handle(UserReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                var error = $"User ID cannot be null or empty: '{request.UserId}'";
                logger.LogWarning(error);
                return new OperationResult<UserResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = [error]
                };
            }

            if (authContext.UserId != request.UserId)
            {
                var error = $"Access denied: User {request.UserId} requested by {authContext.UserId}";
                logger.LogWarning(error);
                return new OperationResult<UserResponse>
                {
                    Status = HttpStatusCode.Forbidden,
                    ErrorMessages = ["User not found"]
                };
            }

            logger.LogInformation("Retrieving user record for user {UserId}", request.UserId);

            var userRecord = await repository.Get(request.UserId);

            if (userRecord == null)
            {
                logger.LogWarning("User record not found for user {UserId}", request.UserId);
                return new OperationResult<UserResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["User not found"]
                };
            }

            // Update tokens if authorization header is present
            var authHeader = contextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                userRecord.Tokens ??= [];
                userRecord.Tokens[IntegrationType.Default] = authHeader;
                logger.LogDebug("Updating authorization token for user {UserId}", request.UserId);

                var updateResult = await repository.Put(userRecord);
                if (!updateResult)
                {
                    logger.LogWarning("Failed to update user record with new token for user {UserId}", request.UserId);
                }
                else
                {
                    logger.LogInformation("Successfully updated user record for user {UserId}", request.UserId);
                }
            }

            var response = new UserResponse
            {
                Id = userRecord.Id,
                Role = userRecord.Role,
                AvatarUrl = userRecord.AvatarUrl,
                IsPublic = userRecord.IsPublic,
                Credits = userRecord.Credits,
            };

            logger.LogInformation("Successfully retrieved user {UserId} with role {Role}", request.UserId, userRecord.Role);

            return new OperationResult<UserResponse>
            {
                Status = HttpStatusCode.OK,
                Data = response
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in UserReadHandler.Handle for user {UserId}", request.UserId);
            return new OperationResult<UserResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred"]
            };
        }
    }
}

