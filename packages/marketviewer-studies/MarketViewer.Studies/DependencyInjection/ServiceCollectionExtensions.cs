using MarketViewer.Studies.Studies;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Studies.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterStudies(this IServiceCollection services)
    {
        return services.AddSingleton<StudyFactory>()
            .AddSingleton<SMA>()
            .AddSingleton<EMA>()
            .AddSingleton<MACD>()
            .AddSingleton<RSI>()
            .AddSingleton<VWAP>()
            .AddSingleton<RVOL>()
            .AddSingleton<MAMR>();
    }
}
