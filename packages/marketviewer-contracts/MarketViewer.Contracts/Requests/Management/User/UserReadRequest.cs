using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.User;

[ExcludeFromCodeCoverage]
public class UserReadRequest : IRequest<OperationResult<UserResponse>>
{
    public string UserId { get; set; }
}

