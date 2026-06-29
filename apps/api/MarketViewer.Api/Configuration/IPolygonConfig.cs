namespace MarketViewer.Core.Interfaces
{
    public interface IPolygonConfig
    {
        public string AggregateUrl { get; set; }
        public string TickersUrl { get; set; }
        public string Token { get; set; }
    }
}
