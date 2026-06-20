using System.Text.RegularExpressions;

namespace StockMountain.MarketData;

public readonly record struct Symbol
{
    private static readonly Regex TickerPattern = new("^[A-Z0-9.]+$", RegexOptions.CultureInvariant);

    public string Value { get; }

    private Symbol(string value) => Value = value;

    public static Symbol Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToUpperInvariant();
        if (!TickerPattern.IsMatch(normalized))
        {
            throw new ArgumentException($"Invalid symbol '{value}'.", nameof(value));
        }

        return new Symbol(normalized);
    }

    public static bool TryCreate(string value, out Symbol symbol)
    {
        symbol = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!TickerPattern.IsMatch(normalized))
        {
            return false;
        }

        symbol = new Symbol(normalized);
        return true;
    }

    public override string ToString() => Value;
}
