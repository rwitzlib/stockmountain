using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyListRequest : IRequest<OperationResult<IEnumerable<StrategyResponse>>>
{
    public VisibilityType? Visibility { get; set; }
}
