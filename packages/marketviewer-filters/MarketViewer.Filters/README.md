# MarketViewer.Filters

A powerful script-based expression engine for technical analysis and market indicator evaluation in the MarketViewer platform.

## 🎯 Overview

MarketViewer.Filters provides a flexible, script-based system for defining complex market analysis conditions and filters. Unlike traditional JSON-based approaches, this system uses intuitive mathematical expressions with support for:

- **Built-in price literals** (`close`, `open`, `high`, `low`, `vwap`, `volume`)
- **Multi-field indicators** (MACD: value, signal, histogram)
- **Dot notation field access** (`macd(12,26,9,ema).signal > 0`)
- **Series-vs-series comparisons** (`close > vwap`, `sma(20) > sma(50)`)
- **Series-based comparisons** with candle range evaluation
- **Timeframe-specific analysis** (`[5m, 3]` for 3 candles on 5-minute chart)
- **Complex logical expressions** (AND, OR, NOT)
- **Built-in technical indicators** (SMA, EMA, MACD)
- **Cross-over detection** (`crosses_over(close, sma(20))`)

## 🚀 Quick Start

```csharp
using MarketViewer.Filters;

// Parse and evaluate expressions
var engine = new IndicatorExpressionEngine();

// Simple comparison with price data
bool result = engine.EvaluateScript("close > vwap", stockData, timeframe);

// Field access
bool bullish = engine.EvaluateScript("macd(12,26,9,ema).histogram > 0", stockData, timeframe);

// Series evaluation over range
bool signal = engine.EvaluateScript("close > sma(20) [, 5]", stockData, timeframe);

// Cross-over detection
bool crossover = engine.EvaluateScript("crosses_over(close, sma(20))", stockData, timeframe);
```

## 📚 Expression Syntax

### Basic Operators
- **Comparison**: `>`, `<`, `>=`, `<=`, `=`, `!=` (supports series-vs-series, series-vs-scalar, and scalar-vs-series)
- **Logical**: `AND`, `OR`, `NOT`

### Function Calls
```javascript
functionName(parameter1, parameter2, ...)
```

### Field Access
```javascript
indicator().fieldName
```

### Timeframe & Range
```javascript
expression [timeframe, range]
```

## 📊 Supported Indicators

### Built-in Price Literals
| Literal | Type | Description |
|---------|------|-------------|
| `close` | Series | Closing prices from stock data |
| `open` | Series | Opening prices from stock data |
| `high` | Series | High prices from stock data |
| `low` | Series | Low prices from stock data |
| `vwap` | Series | VWAP values from stock data |
| `volume` | Series | Volume values from stock data |

### Simple Indicators (Single Field)
| Function | Parameters | Field | Description |
|----------|------------|-------|-------------|
| `sma(n)` | `n`: period | `value` | Simple Moving Average |
| `ema(n)` | `n`: period | `value` | Exponential Moving Average |
| `rsi(period[, overbought, oversold, type])` | `period`: lookback (e.g., 14)<br>`overbought` (optional): e.g., 70<br>`oversold` (optional): e.g., 30<br>`type` (optional): `wilders` (default) / `ema` / `sma` | `value` | Relative Strength Index (0–100) |

### Complex Indicators (Multiple Fields)
| Function | Parameters | Fields | Description |
|----------|------------|--------|-------------|
| `macd(fast, slow, signal, type)` | `fast`: fast period<br>`slow`: slow period<br>`signal`: signal period<br>`type`: "sma"/"ema"/"wilders" | `value`/`macd`: MACD line<br>`signal`: Signal line<br>`histogram`: MACD - Signal | Moving Average Convergence Divergence |
| `support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)` (alias `sr`) | `lookback` (default 250): bars to scan<br>`swing` (default 3): pivot confirmation bars<br>`cluster%` (default 0.8): % band for clustering (0.8 ⇒ 0.8%)<br>`atrMult` (default 0.75): ATR-based zone width multiplier<br>`atrPeriod` (default 14): ATR lookback<br>`minTouches` (default 2): minimum touches to score a zone | `value`: relative distance (positive = closer to resistance)<br>`support`, `resistance`: zone centers<br>`support_strength`, `resistance_strength`: 0–1 strength score<br>`support_distance`, `resistance_distance`: absolute distance from close<br>`support_distance_pct`, `resistance_distance_pct`: distance as % of price<br>`support_zone_width`, `resistance_zone_width`: zone thickness<br>`support_touches`, `resistance_touches`: bounce counts<br>`support_upper`, `support_lower`, `resistance_upper`, `resistance_lower`: band boundaries<br>`near_support`, `near_resistance`: 1 if price sits inside respective zone | Multi-technique support/resistance mapper combining rolling extremes, floor pivots, swing clustering, volume profile, and anchored VWAP |

### Cross-over Functions
| Function | Parameters | Description |
|----------|------------|-------------|
| `crosses_over(series1, series2)` | `series1`, `series2`: Any series expressions | Returns true if series1 crosses above series2 |
| `crosses_under(series1, series2)` | `series1`, `series2`: Any series expressions | Returns true if series1 crosses below series2 |

### Transforms
| Function | Parameters | Description |
|----------|------------|-------------|
| `slope(series [, period])` | `series`: Any series expression<br>`period` (optional, default 5): window size | Linear regression slope over a rolling window; returns a series of slopes |

## 🔍 Field Access Examples

### MACD Field Access
```javascript
// MACD line (same as .value)
macd(12,26,9,ema).macd > 0
macd(12,26,9,ema).value > 0

// Signal line
macd(12,26,9,ema).signal > 0

// Histogram
macd(12,26,9,ema).histogram > 0
```

### Default Field Behavior
```javascript
// These are equivalent:
sma(20) > 100
sma(20).value > 100

// These are equivalent:
macd(12,26,9,ema) > 0
macd(12,26,9,ema).value > 0
```

## 📈 Series Evaluation with Ranges

### Candle Range Syntax
```javascript
expression [, candleRange]
```

Evaluate expression over the last N candles:
```javascript
// Check if SMA(20) > 100 in ANY of the last 5 candles
sma(20) > 100 [, 5]

// Check if MACD histogram > 0 in ANY of the last 3 candles
macd(12,26,9,ema).histogram > 0 [, 3]
```

### Timeframe + Range Syntax
```javascript
expression [timeframe, candleRange]
```

Evaluate on specific timeframe with range:
```javascript
// Check 5-minute chart, last 10 candles
sma(20) > 100 [5m, 10]

// Check 1-hour chart, last 5 candles
macd(12,26,9,ema).signal > 0 [1h, 5]
```

## 💡 Advanced Examples

### Price-based Conditions
```javascript
// Price above VWAP
close > vwap [, 1]

// High breakout above resistance
high > 150 AND close > 148 [, 1]

// Price within range
low > 100 AND high < 200 [, 1]
```

### Complex Trading Conditions
```javascript
// Bullish MACD divergence + oversold RSI
macd(12,26,9,ema).histogram > 0 AND rsi(14) < 30 [, 5]

// Moving average crossover
sma(20).value > sma(50).value [, 3]

// MACD signal crossover
macd(12,26,9,ema).value > macd(12,26,9,ema).signal [, 2]
```

### Cross-over with Price Data
```javascript
// EMA crosses above price level
crosses_over(ema(20), close)

// MACD signal crossover
crosses_over(macd(12,26,9,ema).value, macd(12,26,9,ema).signal)

// Price crosses below moving average
crosses_under(close, sma(50))
```

### Using Slope
```javascript
// Slope of closing price over 5 bars is positive
slope(close, 5) > 0 [, 1]

// Slope of MACD histogram over last 3 bars is increasing
slope(macd(12,26,9,ema).histogram, 3) > 0 [, 1]

// Compare slopes of two series
slope(ema(20), 5) > slope(ema(50), 5) [, 1]
```

## 🧱 Support/Resistance Indicator Guide

The `support_resistance` / `sr` function aggregates several popular techniques to paint realistic support and resistance zones:

- **Rolling extremes** (Donchian-style highest high / lowest low)
- **Classic floor pivots** (previous-period pivot, S1/S2, R1/R2)
- **Swing-point clustering** using fractal pivots
- **Volume-at-price buckets** (lightweight volume profile)
- **Anchored VWAP** from the start of the lookback window

Each candidate level contributes weight based on recency, volume, round-number confluence, and whether the zone was recently respected or broken. Zones are merged into bands with a normalized `strength` score (0–1). Common fields:

| Field | Meaning |
|-------|---------|
| `support`, `resistance` | Center of the strongest zone near price |
| `support_strength`, `resistance_strength` | Relative confidence (0 weak → 1 strong) |
| `support_zone_width`, `resistance_zone_width` | Width of the zone band |
| `support_distance`, `resistance_distance` | Absolute distance from current close |
| `support_distance_pct`, `resistance_distance_pct` | Same distance expressed as % of price |
| `support_touches`, `resistance_touches` | Number of historical touches contributing to the zone |
| `near_support`, `near_resistance` | 1 when price is inside the band (useful for alerts) |

### Usage Examples

```javascript
// Trigger when price sits inside a strong support band
support_resistance().near_support = 1 AND support_resistance().support_strength > 0.65

// Require at least 3 historical touches and a narrow zone
sr(120, 3, 0.6).support_touches >= 3 AND sr().support_zone_width < 1.5

// Look for upside breakouts: price near resistance but strength fading
sr().near_resistance = 1 AND sr().resistance_strength < 0.4 AND close > sr().resistance_upper [, 2]

// Compare relative distance to support vs. resistance
sr().support_distance_pct < sr().resistance_distance_pct
```

Tips:
1. Tune `lookback` and `swing` to match your timeframe. Daily swing traders might start with `sr(250, 3, ...)`, while intraday could shorten to `sr(120, 2, ...)`.
2. Use `cluster%` to adjust how tightly pivots must cluster to form a zone. Larger values produce wider, more forgiving zones.
3. `near_support`/`near_resistance` are binary helpers for quick “price entering zone” filters.
4. `value` returns a signed distance bias (positive when price is closer to resistance, negative when closer to support) for quick comparisons or sorting lists.

### Using RSI
```javascript
// Classic RSI with Wilder smoothing
rsi(14) > 70

// Explicit thresholds and smoothing type (accepted but thresholds are not required)
rsi(14, 70, 30, ema) > 70

// Oversold condition example
rsi(14, 70, 30, wilders) < 30 [, 1]
```

### Multi-Timeframe Analysis
```javascript
// Daily trend up + hourly breakout
sma(200) > sma(200) [1d, 1] AND close > sma(20) [1h, 1]
```

### Risk Management
```javascript
// Stop loss condition
close < entry * 0.95 [, 1]

// Profit target
close > entry * 1.10 [, 1]
```

## 🏗️ Architecture

### Core Components

#### Expression System
- **`IExpression`**: Base interface for all expressions
- **`IExpressionParser`**: Parses script strings into executable expressions
- **`IndicatorExpressionEngine`**: Main evaluation engine
- **`DataAccessExpression`**: Provides access to built-in price data (close, open, high, low, vwap)

#### Result Types
- **`IIndicatorResult`**: Interface for indicator calculation results
- **`SimpleIndicatorResult`**: Single-value indicators (SMA, EMA)
- **`MacdResult`**: Multi-field MACD results

#### Operators
- **Comparison Operators**: Handle series-vs-series, series-vs-scalar, and scalar-vs-series comparisons
- **Logical Operators**: AND, OR, NOT operations
- **Field Access**: Extract specific fields from results
- **Cross-over Functions**: Detect when series cross above/below each other

### Expression Tree
```
Script String → Tokens → AST → Evaluation → Boolean Result
```

## 🧪 Testing

The system includes comprehensive unit tests covering:
- ✅ Expression parsing
- ✅ Built-in price literals (close, open, high, low, vwap, volume)
- ✅ Field access functionality
- ✅ Series-vs-series comparisons
- ✅ Series evaluation with ranges
- ✅ Timeframe parsing with quantities (5m, 1h, 2d)
- ✅ Cross-over detection functions
- ✅ Epsilon-based floating-point equality
- ✅ Error handling

Run tests:
```bash
dotnet test src/MarketViewer.Filters.UnitTests/
```

## 🔄 Migration from JSON Filters

### Before (JSON-based)
```json
{
  "type": "and",
  "conditions": [
    {
      "indicator": "sma",
      "parameters": [20],
      "operator": ">",
      "value": 100
    }
  ]
}
```

### After (Script-based)
```javascript
sma(20) > 100
```

### Migration Benefits
- **80% less code** for typical conditions
- **Intuitive syntax** familiar to traders
- **Multi-field support** for complex indicators
- **Series evaluation** for trend analysis
- **Timeframe flexibility** for multi-timeframe strategies

## 📖 API Reference

### IndicatorExpressionEngine

```csharp
public class IndicatorExpressionEngine
{
    // Evaluate script expression
    bool EvaluateScript(string script, StocksResponse stockData, Timeframe timeframe);

    // Parse expression without evaluation
    IExpression ParseExpression(string script);

    // Compile to a reusable session with incremental evaluation
    FilterSession Compile(string script);
}
```

### FilterSession

```csharp
public class FilterSession
{
    bool Evaluate(StocksResponse stockData, Timeframe timeframe);
    bool EvaluateIncremental(StocksResponse stockData, Timeframe timeframe);
    void Reset();
}
```

### ExpressionContext

```csharp
public class ExpressionContext
{
    public required StocksResponse StockData { get; init; }
    public required Timeframe Timeframe { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
    public int? CandleRange { get; init; }
}
```

### DataAccessExpression

```csharp
public class DataAccessExpression : IExpression
{
    // Provides access to built-in price data
    // Supports: close, open, high, low, vwap
    public DataAccessExpression(string fieldName);
}
```

## 🤝 Contributing

When adding new indicators:

1. **Create Result Class**: Implement `IIndicatorResult` for multi-field indicators
2. **Implement Function**: Create function class implementing `ISeriesFunction`
3. **Register Function**: Add to `ExpressionParser._functions` dictionary
4. **Add Tests**: Comprehensive test coverage for new functionality

Example new indicator:
```csharp
public class RsiResult : BaseIndicatorResult
{
    public double Value { get; set; }

    public override double GetFieldValue(string fieldName = "value") =>
        fieldName.ToLowerInvariant() switch
        {
            "value" => Value,
            _ => throw new ArgumentException($"Field '{fieldName}' not available")
        };

    public override IEnumerable<string> GetAvailableFields() => ["value"];
}
```

## 🔮 Future Improvements

The following enhancements are planned for future versions:

### Expression Engine
- **Parentheses for grouping**: Support for `(A AND B) OR C` style expressions with proper precedence control

### Performance & Caching
- **Functional result caching**: Cache indicator calculations across evaluations to avoid redundant computations when the same function appears multiple times in an expression

### Developer Experience
- **Robust error messages**: More detailed error reporting with example usage suggestions and better context for parsing failures

## 🚨 Important Notes

- **Field names are case-insensitive**: `value`, `VALUE`, `Value` all work
- **Default field is "value"**: Can be omitted in most expressions
- **Series evaluation**: Checks if ANY value in range satisfies condition
- **Series alignment**: Indicators with different warm-up periods are automatically aligned by length
- **Epsilon equality**: Floating-point comparisons use epsilon (1e-9) for precision handling
- **Timeframe syntax**: Supports quantities (5m, 1h, 2d) and standard formats (1m, 1h, 1d)
- **Price literals**: `close`, `open`, `high`, `low`, `vwap`, `volume` work directly without parentheses
- **Cross-over detection**: Automatically handles series of different lengths

## 📄 License

This project is part of the MarketViewer platform. See main project license for details.
