using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market
{
    [ExcludeFromCodeCoverage]
    public class ScanResponse
    {
        public ScanResponse()
        {
            Items = new List<Item>();
        }

        public IEnumerable<Item> Items { get; set; }
        public long TimeElapsed { get; set; }

        public class Item
        {
            public string Ticker { get; set; }
            public float Price { get; set; }
            public float Volume { get; set; }
            public long? Float { get; set; }
        }
    }
}
