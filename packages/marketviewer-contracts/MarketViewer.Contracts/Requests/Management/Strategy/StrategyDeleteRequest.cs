using MarketViewer.Contracts.Models;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyDeleteRequest : IRequest<OperationResult<bool>>
{
    public string Id { get; set; }
}
