using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketViewer.Contracts.UnitTests.Responses
{
    public class StocksResponseUnitTests
    {
        [Fact]
        public void Clone_Should_Not_Modify_Original()
        {

            var original = new StocksResponse
            {
                Ticker = "SPY",
                Results = new List<Bar>
                {
                    new Bar
                    {
                        Close = 100
                    }
                }
            };

            var clone = original.Clone();
            clone.Results[0].Close = 200;
            clone.Results.Add(new Bar
            {
                Close = 300
            });

            Assert.Equal("SPY", original.Ticker);
            Assert.Equal(100, original.Results[0].Close);
            Assert.Single(original.Results);
        }
    }
}
