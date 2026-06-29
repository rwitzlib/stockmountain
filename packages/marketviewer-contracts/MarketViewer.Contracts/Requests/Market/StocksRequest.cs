using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MediatR;

namespace MarketViewer.Contracts.Requests.Market;

[ExcludeFromCodeCoverage]
public class StocksRequest : BaseRequest, IRequest<OperationResult<StocksResponse>>
{
    /// <summary>
    /// The ticker symbol of the stock/equity.
    /// </summary>
    [Required]
    [StringLength(6)]
    public string Ticker { get; set; }

    /// <summary>
    /// The size of the timespan multiplier.
    /// </summary>
    [Required]
    public int Multiplier { get; set; }

    /// <summary>
    /// The size of the time window.
    /// </summary>
    [Required]
    public Timespan Timespan { get; set; }

    /// <summary>
    /// The start of the aggregate time window. Either a date with the format YYYY-MM-DD or 
    /// a millisecond timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset From { get; set; }

    /// <summary>
    /// The end of the aggregate time window. Either a date with the format YYYY-MM-DD or 
    /// a millisecond timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset To { get; set; }

    public List<Indicator> Indicators { get; set; }

    [Range(1, 50000)]
    public int Limit { get; set; } = 50000;
}
