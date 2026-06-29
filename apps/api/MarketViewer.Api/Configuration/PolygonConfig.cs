using MarketViewer.Core.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Api.Configuration
{
    [ExcludeFromCodeCoverage]
    public class PolygonConfig : IPolygonConfig
    {
        public string AggregateUrl { get; set; }
        public string TickersUrl { get; set; }
        public string Token { get; set; }
    }
}
