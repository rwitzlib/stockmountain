using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Tools;

[ExcludeFromCodeCoverage]
public class SnapshotRequest : BaseRequest
{
    /// <summary>
    /// Comma-separated list of tickers to filter the snapshot.
    /// </summary>
    [Required]
    public string Tickers { get; set; }
}
