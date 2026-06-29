//using FluentAssertions;
//using MarketViewer.Contracts.Dtos;
//using MarketViewer.Contracts.Enums;
//using MarketViewer.Contracts.Enums.Scan;
//using MarketViewer.Contracts.Models.Scan;
//using MarketViewer.Contracts.Models.Scan.Operands;
//using System.Text.Json;

//namespace MarketViewer.Contracts.UnitTests.Mappers;

//public class ScanArgumentMapperUnitTests
//{
//    [Fact]
//    public void Map_ShouldReturnCorrectScanArgument()
//    {
//        // Arrange
//        var scanArgumentRequest = new ScanArgumentDto
//        {
//            Operator = "AND",
//            Filters =
//            [
//                new()
//                {
//                    CollectionModifier = "ALL",
//                    FirstOperand = new OperandDto
//                    {
//                        Type = OperandType.PriceAction,
//                        Name = "Close",
//                        Modifier = OperandModifier.Value,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Operator = FilterOperator.gt,
//                    SecondOperand = new OperandDto
//                    {
//                        Type = OperandType.PriceAction,
//                        Name = "Open",
//                        Modifier = OperandModifier.Slope,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Timeframe = new Timeframe(1, Timespan.minute)
//                },
//                new()
//                {
//                    FirstOperand = new OperandDto
//                    {
//                        Type = OperandType.Study,
//                        Name = "macd",
//                        Parameters = "12,26,9,ema",
//                        Modifier = OperandModifier.Value,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Operator = FilterOperator.gt,
//                    SecondOperand = new OperandDto
//                    {
//                        Type = OperandType.Fixed,
//                        Value = 10000
//                    },
//                    Timeframe = new Timeframe(1, Timespan.minute)
//                }
//            ]
//        };

//        // Act
//        var result = ScanArgumentMapper.ConvertFromScanArgumentDto(scanArgumentRequest);

//        // Assert
//        result.Should().NotBeNull();
//        result.Should().BeOfType<ScanArgument>();

//        result.Operator.Should().Be("AND");
//        result.Filters.Should().NotBeNull();
//        result.Filters.Should().HaveCount(2);

//        result.Filters[0].CollectionModifier.Should().Be("ALL");
//        result.Filters[0].FirstOperand.Should().NotBeNull();
//        result.Filters[0].FirstOperand.Should().BeOfType<PriceActionOperand>();
//        result.Filters[0].FirstOperand.Should().BeEquivalentTo(new PriceActionOperand
//        {
//            PriceAction = PriceActionType.close,
//            Modifier = OperandModifier.Value,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[0].Operator.Should().Be(FilterOperator.gt);
//        result.Filters[0].SecondOperand.Should().NotBeNull();
//        result.Filters[0].SecondOperand.Should().BeOfType<PriceActionOperand>();
//        result.Filters[0].SecondOperand.Should().BeEquivalentTo(new PriceActionOperand
//        {
//            PriceAction = PriceActionType.open,
//            Modifier = OperandModifier.Slope,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[0].Timeframe.Should().BeEquivalentTo(new Timeframe(1, Timespan.minute));

//        result.Filters[1].FirstOperand.Should().NotBeNull();
//        result.Filters[1].FirstOperand.Should().BeOfType<StudyOperand>();
//        result.Filters[1].FirstOperand.Should().BeEquivalentTo(new StudyOperand
//        {
//            Study = StudyType.macd,
//            Parameters = "12,26,9,ema",
//            Modifier = OperandModifier.Value,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[1].Operator.Should().Be(FilterOperator.gt);
//        result.Filters[1].SecondOperand.Should().NotBeNull();
//        result.Filters[1].SecondOperand.Should().BeOfType<FixedOperand>();
//        result.Filters[1].SecondOperand.Should().BeEquivalentTo(new FixedOperand
//        {
//            Value = 10000
//        });
//        result.Filters[1].Timeframe.Should().BeEquivalentTo(new Timeframe(1, Timespan.minute));
//    }

//    [Fact]
//    public void Map_ShouldReturnCorrectScanArgumentDetails()
//    {
//        // Arrange
//        var scanArgument = new ScanArgument
//        {
//            Operator = "AND",
//            Filters =
//            [
//                new()
//                {
//                    CollectionModifier = "ALL",
//                    FirstOperand = new PriceActionOperand
//                    {
//                        PriceAction = PriceActionType.close,
//                        Modifier = OperandModifier.Value,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Operator = FilterOperator.gt,
//                    SecondOperand = new PriceActionOperand
//                    {
//                        PriceAction = PriceActionType.open,
//                        Modifier = OperandModifier.Slope,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Timeframe = new Timeframe(1, Timespan.minute)
//                },
//                new()
//                {
//                    FirstOperand = new StudyOperand
//                    {
//                        Study  = StudyType.macd,
//                        Parameters = "12,26,9,ema",
//                        Modifier = OperandModifier.Value,
//                        Timeframe = new Timeframe(1, Timespan.minute)
//                    },
//                    Operator = FilterOperator.gt,
//                    SecondOperand = new FixedOperand
//                    {
//                        Value = 10000
//                    },
//                    Timeframe = new Timeframe(1, Timespan.minute)
//                }
//            ]
//        };

//        // Act
//        var result = ScanArgumentMapper.ConvertToScanArgumentDto(scanArgument);

//        // Assert
//        result.Should().NotBeNull();
//        result.Should().BeOfType<ScanArgumentDto>();

//        result.Operator.Should().Be("AND");
//        result.Filters.Should().NotBeNull();
//        result.Filters.Should().HaveCount(2);

//        result.Filters[0].CollectionModifier.Should().Be("ALL");
//        result.Filters[0].FirstOperand.Should().NotBeNull();
//        result.Filters[0].FirstOperand.Should().BeOfType<OperandDto>();
//        result.Filters[0].FirstOperand.Should().BeEquivalentTo(new OperandDto
//        {
//            Type = OperandType.PriceAction,
//            Name = "close",
//            Modifier = OperandModifier.Value,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[0].Operator.Should().Be(FilterOperator.gt);
//        result.Filters[0].SecondOperand.Should().NotBeNull();
//        result.Filters[0].SecondOperand.Should().BeOfType<OperandDto>();
//        result.Filters[0].SecondOperand.Should().BeEquivalentTo(new OperandDto
//        {
//            Type = OperandType.PriceAction,
//            Name = "open",
//            Modifier = OperandModifier.Slope,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[0].Timeframe.Should().BeEquivalentTo(new Timeframe(1, Timespan.minute));

//        result.Filters[1].FirstOperand.Should().NotBeNull();
//        result.Filters[1].FirstOperand.Should().BeOfType<OperandDto>();
//        result.Filters[1].FirstOperand.Should().BeEquivalentTo(new OperandDto
//        {
//            Type = OperandType.Study,
//            Name = "macd",
//            Parameters = "12,26,9,ema",
//            Modifier = OperandModifier.Value,
//            Timeframe = new Timeframe(1, Timespan.minute)
//        });
//        result.Filters[1].Operator.Should().Be(FilterOperator.gt);
//        result.Filters[1].SecondOperand.Should().NotBeNull();
//        result.Filters[1].SecondOperand.Should().BeOfType<OperandDto>();
//        result.Filters[1].SecondOperand.Should().BeEquivalentTo(new OperandDto
//        {
//            Type = OperandType.Fixed,
//            Value = 10000
//        });
//        result.Filters[1].Timeframe.Should().BeEquivalentTo(new Timeframe(1, Timespan.minute));
//    }

//    [Fact]
//    public void Map_From_Json()
//    {
//        var json = "{\r\n          \"type\": \"Study\",\r\n          \"name\": \"rsi\",\r\n          \"parameters\": \"14,70,30,ema\",\r\n          \"modifier\": \"Value\",\r\n          \"timeframe\": {\r\n            \"multiplier\": 1,\r\n            \"timespan\": \"minute\"\r\n          }\r\n        }";
//        var options = new JsonSerializerOptions
//        {
//            PropertyNameCaseInsensitive = true
//        };

//        var result = JsonSerializer.Deserialize<OperandDto>(json, options);
//    }
//}