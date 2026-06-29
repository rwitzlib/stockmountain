using MarketViewer.Contracts.Enums.Scan;
using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Dtos;

[ExcludeFromCodeCoverage]
public class FilterDto
{
    public string CollectionModifier { get; set; }
    public OperandDto FirstOperand { get; set; }
    public FilterOperator Operator { get; set; }
    public OperandDto SecondOperand { get; set; }
    public Timeframe Timeframe { get; set; }
}
