using FluentAssertions;
using MarketViewer.Core.Utilities;
using Xunit;

namespace MarketViewer.Infrastructure.UnitTests.Extension
{
    public class DateTimExtensionsUnitTests
    {
        [Fact]
        public static void Date_Is_Converted_To_Start_Of_Day()
        {
            // Arrange
            var dateTime = DateTimeOffset.Now;

            // Act
            var response = DateUtilities.StartOfDay(dateTime);

            // Assert
            response.Year.Should().Be(dateTime.Year);
            response.Month.Should().Be(dateTime.Month);
            response.Day.Should().Be(dateTime.Day);

            response.Hour.Should().Be(0);
            response.Minute.Should().Be(0);
        }
        
        [Fact]
        public static void Date_Is_Converted_To_End_Of_Day()
        {
            // Arrange
            var dateTime = DateTimeOffset.Now;
            
            // Act
            var response = DateUtilities.EndOfDay(dateTime);

            // Assert
            response.Year.Should().Be(dateTime.Year);
            response.Month.Should().Be(dateTime.Month);
            response.Day.Should().Be(dateTime.Day);

            response.Hour.Should().Be(23);
            response.Minute.Should().Be(59);
        }
    }
}
