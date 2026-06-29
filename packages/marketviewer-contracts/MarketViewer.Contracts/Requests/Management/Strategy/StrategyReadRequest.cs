using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyReadRequest : IRequest<OperationResult<StrategyResponse>>
{
    public string Id { get; set; }
}
