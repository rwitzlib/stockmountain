using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Scan;
using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Dtos;

[ExcludeFromCodeCoverage]
public class OperandDto
{
    public OperandType Type { get; set; }
    public string Name { get; set; }
    public string Parameters { get; set; }
    public OperandModifier? Modifier { get; set; }
    public Timeframe Timeframe { get; set; }
    public float? Value { get; set; }
}
