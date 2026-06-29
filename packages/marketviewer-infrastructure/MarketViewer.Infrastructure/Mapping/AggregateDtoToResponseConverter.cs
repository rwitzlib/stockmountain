//using AutoMapper;
//using MarketViewer.Contracts.Responses.Market;
//using Polygon.Client.Models;
//using System.Collections.Generic;
//using System.Linq;

//namespace MarketViewer.Infrastructure.Mapping
//{
//    public class AggregateDtoToResponseConverter : ITypeConverter<IEnumerable<AggregateDto>, StocksResponse>
//    {
//        public StocksResponse Convert(IEnumerable<AggregateDto> source, StocksResponse destination, ResolutionContext context)
//        {
//            if (source is null || !source.Any())
//            {
//                return null;
//            }

//            var aggregates = source.ToArray();

//            var candles = new List<Bar>();

//            for (var i = 0; i < aggregates.Length; i++)
//            {
//                if (aggregates[i].Ticker is null)
//                {
//                    return null;
//                }

//                if (i > 0 && !aggregates[i].Ticker.Equals(aggregates[i - 1].Ticker))
//                {
//                    return null;
//                }

//                if (aggregates[i].Results is null)
//                {
//                    continue;
//                }

//                candles.AddRange(aggregates[i].Results);
//            }

//            return new StocksResponse
//            {
//                Ticker = aggregates[0].Ticker,
//                Results = candles,
//            };
//        }
//    }
//}
