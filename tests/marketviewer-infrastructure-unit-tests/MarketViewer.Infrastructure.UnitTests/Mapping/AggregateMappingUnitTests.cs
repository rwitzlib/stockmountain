using Xunit;
using AutoMapper;
using MarketViewer.Infrastructure.Mapping;

namespace MarketViewer.Infrastructure.UnitTests.Mappings
{
    public class AggregateMappingUnitTests
    {
        [Fact]
        public void Check_Aggregate_Mapping_Configuration()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.AddProfile<AggregateProfile>());

            configuration.AssertConfigurationIsValid();
        }
    }
}
