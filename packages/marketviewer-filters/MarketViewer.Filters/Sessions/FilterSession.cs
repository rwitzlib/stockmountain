using System.Runtime.CompilerServices;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Operators.Logical;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters.Parsing;

namespace MarketViewer.Filters.Sessions;

/// <summary>
/// Maintains per-expression caches and performs incremental evaluation when possible.
/// </summary>
public class FilterSession
{
    private readonly IExpression _root;

    private sealed class NodeCache
    {
        public object? Result { get; set; }
        public int DataCount { get; set; }
        public long LastTimestamp { get; set; }
    }

    private readonly Dictionary<IExpression, NodeCache> _cache = new(ReferenceEqualityComparer<IExpression>.Instance);

    // Session-level data tracking
    private int _lastDataCount;
    private long _lastTimestamp;

    public FilterSession(IExpression root)
    {
        _root = root;
    }

    public void Reset()
    {
        _cache.Clear();
        _lastDataCount = 0;
        _lastTimestamp = 0;
    }

    public bool Evaluate(StocksResponse stockData, Timeframe timeframe, Dictionary<string, object>? parameters = null, DateTimeOffset? evaluationTime = null)
    {
        Reset();
        var ctx = new ExpressionContext { StockData = stockData, Timeframe = timeframe, Parameters = parameters, EvaluationTime = evaluationTime };
        var value = EvaluateNode(_root, ctx, incremental: false);
        _lastDataCount = stockData.Results.Count;
        _lastTimestamp = stockData.Results.LastOrDefault()?.Timestamp ?? 0;
        return ToBool(value);
    }

    public bool EvaluateIncremental(StocksResponse stockData, Timeframe timeframe, Dictionary<string, object>? parameters = null, DateTimeOffset? evaluationTime = null)
    {
        return ToBool(EvaluateIncrementalRaw(stockData, timeframe, parameters, evaluationTime));
    }

    /// <summary>
    /// Incremental evaluation that returns the root node's raw value (a series, scalar or bool)
    /// instead of coercing to bool. Used by golden tests to compare incremental and full series.
    /// </summary>
    public object EvaluateIncrementalRaw(StocksResponse stockData, Timeframe timeframe, Dictionary<string, object>? parameters = null, DateTimeOffset? evaluationTime = null)
    {
        var count = stockData.Results.Count;
        var lastTs = count > 0 ? stockData.Results[^1].Timestamp : 0;

        // Detect rewind or non-append changes; fallback to full
        bool canAppend = count >= _lastDataCount && lastTs >= _lastTimestamp;
        if (!canAppend)
        {
            Reset();
        }

        var ctx = new ExpressionContext { StockData = stockData, Timeframe = timeframe, Parameters = parameters, EvaluationTime = evaluationTime };
        var value = EvaluateNode(_root, ctx, incremental: canAppend);

        _lastDataCount = count;
        _lastTimestamp = lastTs;
        return value;
    }

    private static bool ToBool(object result)
    {
        return result switch
        {
            bool b => b,
            double d => d > 0,
            float f => f > 0,
            int i => i > 0,
            _ => false
        };
    }

    private object EvaluateNode(IExpression expr, ExpressionContext ctx, bool incremental)
    {
        switch (expr)
        {
            case TimeframeRangeExpression tf:
                var modified = new ExpressionContext
                {
                    StockData = ctx.StockData,
                    Timeframe = tf.GetTimeframe() ?? ctx.Timeframe,
                    Parameters = ctx.Parameters,
                    CandleRange = tf.GetRange() ?? ctx.CandleRange,
                    RangeEvaluationMode = tf.GetRangeEvaluationMode() ?? ctx.RangeEvaluationMode,
                    EvaluationTime = ctx.EvaluationTime
                };
                return EvaluateNode(tf.GetInnerExpression(), modified, incremental);

            case DataAccessExpression data:
                return EvaluateDataAccess(expr, data, ctx, incremental);

            case FieldAccessExpression field:
                return EvaluateFieldAccess(expr, field, ctx, incremental);

            case FunctionCallExpression call:
                return EvaluateFunctionCall(expr, call, ctx, incremental);

            case UnaryExpression unary:
                return EvaluateUnary(expr, unary, ctx, incremental);

            case BinaryExpression binary:
                return EvaluateBinary(expr, binary, ctx, incremental);

            case LiteralExpression literal:
                return EvaluateLiteral(expr, literal, ctx);

            default:
                var res = expr.Evaluate(ctx);
                _cache[expr] = new NodeCache { Result = res, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
                return res;
        }
    }

    private object EvaluateDataAccess(IExpression key, DataAccessExpression expr, ExpressionContext ctx, bool incremental)
    {
        if (expr.IsScalar)
        {
            if (_cache.TryGetValue(key, out var scalarEntry))
            {
                return scalarEntry.Result!;
            }

            var scalar = expr.Evaluate(ctx);
            _cache[key] = new NodeCache
            {
                Result = scalar,
                DataCount = ctx.StockData.Results.Count,
                LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0
            };
            return scalar;
        }

        // "time" is the evaluation clock — a single element that changes every
        // evaluation. Append-caching it would freeze the clock at its first value,
        // so a stale ticker would keep passing a time gate forever.
        if (expr.GetFieldName() == "time")
        {
            var timeResult = expr.Evaluate(ctx);
            _cache[key] = new NodeCache { Result = timeResult, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
            return timeResult;
        }

        var data = ctx.StockData.Results;
        if (!_cache.TryGetValue(key, out var entry) || !incremental || entry.Result is not List<IIndicatorResult> prevSeries)
        {
            var full = (List<IIndicatorResult>)expr.Evaluate(ctx);
            _cache[key] = new NodeCache { Result = full, DataCount = data.Count, LastTimestamp = data.LastOrDefault()?.Timestamp ?? 0 };
            return full;
        }

        // Append new bars only
        int prevCount = prevSeries.Count;
        if (data.Count < prevCount)
        {
            // rewind; rebuild
            var full = (List<IIndicatorResult>)expr.Evaluate(ctx);
            _cache[key] = new NodeCache { Result = full, DataCount = data.Count, LastTimestamp = data.LastOrDefault()?.Timestamp ?? 0 };
            return full;
        }

        // The last bar we read may have been forming at the time (mutated in place since, e.g. the
        // backtester's UpdateLatestCandle on a [5m] scan) — and this node may have skipped some
        // evaluations (short-circuit), so that bar is not necessarily still the last one. Always
        // re-read the last cached bar, then append any new bars (see Functions.IncrementalSeries).
        if (prevCount > 0)
        {
            prevSeries[prevCount - 1] = expr.CreateBarResult(data[prevCount - 1]);
        }
        for (int i = prevCount; i < data.Count; i++)
        {
            prevSeries.Add(expr.CreateBarResult(data[i]));
        }

        entry.DataCount = data.Count;
        entry.LastTimestamp = data.LastOrDefault()?.Timestamp ?? 0;
        SeriesAlignment.AssertTail(prevSeries, ctx, expr.GetFieldName());
        return prevSeries;
    }

    private object EvaluateFieldAccess(IExpression key, FieldAccessExpression field, ExpressionContext ctx, bool incremental)
    {
        // time.hour / time.minute derive from the evaluation clock; the append path
        // below would return the first evaluation's value forever (both series stay
        // at one element, so nothing ever appends). Always evaluate fresh.
        if (field.GetTargetExpression() is DataAccessExpression timeTarget && timeTarget.GetFieldName() == "time")
        {
            var timeFieldResult = field.Evaluate(ctx);
            _cache[key] = new NodeCache { Result = timeFieldResult, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
            return timeFieldResult;
        }

        var targetObj = EvaluateNode(field.GetTargetExpression(), ctx, incremental);

        if (!_cache.TryGetValue(key, out var entry) || !incremental)
        {
            var full = field.Evaluate(ctx);
            _cache[key] = new NodeCache { Result = full, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
            return full;
        }

        // Try to append if target is a series and prior result is a List<double>
        if (targetObj is List<IIndicatorResult> targetSeries && entry.Result is List<double> prevDoubles)
        {
            int prevCount = prevDoubles.Count;
            if (targetSeries.Count < prevCount)
            {
                var full = field.Evaluate(ctx);
                _cache[key] = new NodeCache { Result = full, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
                return full;
            }

            // The target's last cached point may have been re-priced (forming bar): always re-read it.
            if (prevCount > 0)
            {
                prevDoubles[prevCount - 1] = targetSeries[prevCount - 1].GetFieldValue(field.GetFieldName());
            }
            for (int i = prevCount; i < targetSeries.Count; i++)
            {
                prevDoubles.Add(targetSeries[i].GetFieldValue(field.GetFieldName()));
            }

            entry.DataCount = ctx.StockData.Results.Count;
            entry.LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0;
            return prevDoubles;
        }

        // Fallback
        var fallback = field.Evaluate(ctx);
        _cache[key] = new NodeCache { Result = fallback, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
        return fallback;
    }

    private object EvaluateFunctionCall(IExpression key, FunctionCallExpression call, ExpressionContext ctx, bool incremental)
    {
        // Evaluate args first (so child caches update)
        var argExprs = call.GetArguments();
        var argValues = new object[argExprs.Count];
        for (int i = 0; i < argExprs.Count; i++)
        {
            argValues[i] = EvaluateNode(argExprs[i], ctx, incremental);
        }

        var func = call.GetFunction();
        object result;

        if (incremental && func is IIncrementalSeriesFunction incr && _cache.TryGetValue(key, out var entry) && entry.Result is object prevRes)
        {
            result = incr.Append(argValues, ctx, prevRes);
        }
        else
        {
            result = func.Execute(argValues, ctx);
        }

        // The direct path checks inside FunctionCallExpression.Evaluate; here Execute/Append are
        // called straight on the function, so check the result once (covers the incremental path).
        if (result is List<IIndicatorResult> series)
        {
            SeriesAlignment.AssertTail(series, ctx, func.Name);
        }

        _cache[key] = new NodeCache { Result = result, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
        return result;
    }

    private object EvaluateUnary(IExpression key, UnaryExpression unary, ExpressionContext ctx, bool incremental)
    {
        var operandValue = EvaluateNode(unary.Operand, ctx, incremental);
        var result = unary.Operator.Execute(null, operandValue, ctx);
        _cache[key] = new NodeCache { Result = result, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
        return result;
    }

    private object EvaluateBinary(IExpression key, BinaryExpression binary, ExpressionContext ctx, bool incremental)
    {
        object result;

        if (binary.Operator is ILogicalOperator)
        {
            var leftInfo = ExpressionPlanner.Analyze(binary.Left);
            var rightInfo = ExpressionPlanner.Analyze(binary.Right);

            bool isAnd = string.Equals(binary.Operator.Symbol, "AND", StringComparison.OrdinalIgnoreCase);
            bool isOr = string.Equals(binary.Operator.Symbol, "OR", StringComparison.OrdinalIgnoreCase);

            IExpression first;
            IExpression second;

            if (isAnd)
            {
                bool pickLeftFirst = leftInfo.SelectivityHint <= rightInfo.SelectivityHint ||
                    (Math.Abs(leftInfo.SelectivityHint - rightInfo.SelectivityHint) < 1e-6 && leftInfo.EstimatedCost <= rightInfo.EstimatedCost);

                first = pickLeftFirst ? binary.Left : binary.Right;
                second = pickLeftFirst ? binary.Right : binary.Left;

                var firstValue = Convert.ToBoolean(EvaluateNode(first, ctx, incremental));
                if (!firstValue)
                {
                    result = false;
                }
                else
                {
                    var secondValue = Convert.ToBoolean(EvaluateNode(second, ctx, incremental));
                    result = firstValue && secondValue;
                }
            }
            else if (isOr)
            {
                bool pickLeftFirst = leftInfo.SelectivityHint >= rightInfo.SelectivityHint ||
                    (Math.Abs(leftInfo.SelectivityHint - rightInfo.SelectivityHint) < 1e-6 && leftInfo.EstimatedCost <= rightInfo.EstimatedCost);

                first = pickLeftFirst ? binary.Left : binary.Right;
                second = pickLeftFirst ? binary.Right : binary.Left;

                var firstValue = Convert.ToBoolean(EvaluateNode(first, ctx, incremental));
                if (firstValue)
                {
                    result = true;
                }
                else
                {
                    var secondValue = Convert.ToBoolean(EvaluateNode(second, ctx, incremental));
                    result = firstValue || secondValue;
                }
            }
            else
            {
                // Other logical operators (e.g., NOT) handled elsewhere
                var leftVal = EvaluateNode(binary.Left, ctx, incremental);
                var rightVal = EvaluateNode(binary.Right, ctx, incremental);
                result = binary.Operator.Execute(leftVal, rightVal, ctx);
            }
        }
        else
        {
            // The evaluation clock is one value per evaluation: no candle window (see BinaryExpression.Evaluate).
            var comparisonCtx = ExpressionShape.IsClock(binary.Left) || ExpressionShape.IsClock(binary.Right)
                ? ctx.ForSingleCandle()
                : ctx;
            var leftVal = EvaluateNode(binary.Left, comparisonCtx, incremental);
            var rightVal = EvaluateNode(binary.Right, comparisonCtx, incremental);
            result = binary.Operator.Execute(leftVal, rightVal, comparisonCtx);
        }

        _cache[key] = new NodeCache { Result = result, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
        return result;
    }

    private object EvaluateLiteral(IExpression key, LiteralExpression literal, ExpressionContext ctx)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            return entry.Result!;
        }

        var result = literal.Evaluate(ctx);
        _cache[key] = new NodeCache { Result = result, DataCount = ctx.StockData.Results.Count, LastTimestamp = ctx.StockData.Results.LastOrDefault()?.Timestamp ?? 0 };
        return result;
    }
}

/// <summary>
/// Reference equality comparer to key dictionaries by actual node instances.
/// </summary>
internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static readonly ReferenceEqualityComparer<T> Instance = new();
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
