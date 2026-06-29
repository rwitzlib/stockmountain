using AutoMapper;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Requests;
using Polygon.Client.Responses;

namespace MarketViewer.Infrastructure.Mapping;

public class AggregateProfile : Profile
{
    public AggregateProfile()
    {
        //CreateMap<IEnumerable<AggregateDto>, StocksResponse>()
        //    .ConvertUsing<AggregateDtoToResponseConverter>();

        //CreateMap<AggregateDto, StocksResponse>()
        //    .ForMember(dest => dest.Status, opt => opt.Ignore())
        //    .ForMember(dest => dest.Studies, opt => opt.Ignore())
        //    .ForMember(dest => dest.TickerInfo, opt => opt.Ignore());

        CreateMap<StocksRequest, PolygonAggregateRequest>()
            .ForMember(dest => dest.Ticker, opt => opt.MapFrom(src => src.Ticker.ToUpperInvariant()))
            .ForMember(dest => dest.Timespan, opt => opt.MapFrom(src => src.Timespan.ToString().ToLowerInvariant()))
            .ForMember(dest => dest.From, opt => opt.MapFrom(src => src.From.ToUnixTimeMilliseconds()))
            .ForMember(dest => dest.To, opt => opt.MapFrom(src => src.To.ToUnixTimeMilliseconds()))
            .ForMember(dest => dest.Adjusted, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.Sort, opt => opt.MapFrom(src => "asc"))
            .ForMember(dest => dest.Limit, opt => opt.MapFrom(src => src.Limit));

        CreateMap<PolygonAggregateResponse, StocksResponse>()
            .ForMember(dest => dest.Indicators, opt => opt.Ignore())
            .ForMember(dest => dest.TickerInfo, opt => opt.Ignore());
    }
}
