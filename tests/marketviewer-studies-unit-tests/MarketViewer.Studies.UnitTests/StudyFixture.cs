using MarketViewer.Contracts.Caching;
using MarketViewer.Studies.Studies;
using Moq;
using Polygon.Client.Interfaces;
using Xunit;

namespace MarketViewer.Studies.UnitTests;

public class StudyFixture : IClassFixture<StudyFactory>
{
    public StudyFixture()
    {
        MarketCache = new Mock<IMarketCache>();
        var polygonClient = new Mock<IPolygonClient>();
        StudyFactory = new StudyFactory(new SMA(), new EMA(), new MACD(), new RSI(), new VWAP(), new RVOL(MarketCache.Object), new MAMR());
    }

    public TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public StudyFactory StudyFactory { get; private set; }
    public Mock<IMarketCache> MarketCache { get; private set; }
}
