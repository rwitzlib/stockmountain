using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Composite support/resistance indicator that fuses multiple techniques:
///  - rolling extremes (Donchian-style bands)
///  - classic floor pivots (previous-period high/low/close)
///  - swing point clustering (fractals/pivots)
///  - basic volume-at-price profiling + anchored VWAP
/// The indicator returns a series where each point exposes the strongest support/resistance
/// zone available at that timestamp along with metadata (strength, distance, zone width, etc).
/// Usage: support_resistance(lookback, swing, clusterPercent, atrMultiplier, atrPeriod, minTouches)
/// All parameters are optional; pass fewer values to rely on defaults.
/// </summary>
public class SupportResistanceFunction : ISeriesFunction
{
    private const double Epsilon = 1e-6;

    public string Name => "support_resistance";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        var bars = context.StockData.Results;
        if (bars.Count < 10)
        {
            return new List<IIndicatorResult>();
        }

        var (lookback, swingSpan, clusterPercent, atrMultiplier, atrPeriod, minTouches) = ParseParameters(parameters);
        var atrSeries = ComputeAtrSeries(bars, atrPeriod);
        var clusterFraction = clusterPercent / 100.0;
        var results = new List<IIndicatorResult>();

        for (int i = 0; i < bars.Count; i++)
        {
            int windowStart = Math.Max(0, i - lookback + 1);
            int windowLength = i - windowStart + 1;
            if (windowLength < swingSpan * 2 + 5)
            {
                continue;
            }

            var (windowHigh, windowLow, avgVolume) = ComputeWindowStats(bars, windowStart, i);
            int pivotWindowStart = Math.Max(windowStart, i - Math.Max(10, swingSpan * 3));
            var pivotLevels = ComputePivotLevels(bars, pivotWindowStart, i - 1);
            var confluenceLevels = BuildConfluenceLevels(windowHigh, windowLow, pivotLevels);

            double close = bars[i].Close;
            double tolerance = Math.Max(Math.Abs(close) * clusterFraction, atrSeries[i] * atrMultiplier);
            tolerance = Math.Max(tolerance, (windowHigh - windowLow) * 0.01);
            tolerance = Math.Max(tolerance, 0.01);

            var supportCandidates = new List<LevelCandidate>();
            var resistanceCandidates = new List<LevelCandidate>();

            AddRollingExtremeCandidates(supportCandidates, resistanceCandidates, windowLow, windowHigh, tolerance, i, bars[i].Timestamp);
            AddPivotCandidates(supportCandidates, resistanceCandidates, pivotLevels, tolerance, i, bars[i].Timestamp, close);
            GatherSwingCandidates(
                supportCandidates,
                resistanceCandidates,
                bars,
                windowStart,
                i,
                swingSpan,
                tolerance,
                clusterFraction,
                avgVolume,
                confluenceLevels,
                lookback,
                atrSeries);
            AddVolumeProfileCandidates(supportCandidates, resistanceCandidates, bars, windowStart, i, tolerance, avgVolume, close);
            AddAnchoredVwapCandidate(supportCandidates, resistanceCandidates, bars, windowStart, i, tolerance, close);

            var supportZones = BuildZones(supportCandidates, tolerance, minTouches, true, i, windowStart);
            var resistanceZones = BuildZones(resistanceCandidates, tolerance, minTouches, false, i, windowStart);

            ApplyBrokenPenalty(supportZones, bars, windowStart, i);
            ApplyBrokenPenalty(resistanceZones, bars, windowStart, i);

            var supportZone = PickZone(supportZones, close, true);
            var resistanceZone = PickZone(resistanceZones, close, false);

            var result = BuildResult(bars[i], supportZone, resistanceZone);
            results.Add(result);
        }

        return results;
    }

    private static (int Lookback, int Swing, double ClusterPercent, double AtrMultiplier, int AtrPeriod, int MinTouches) ParseParameters(object[] parameters)
    {
        if (parameters.Length > 6)
        {
            throw new ArgumentException("support_resistance accepts up to 6 parameters: (lookback, swing, clusterPercent, atrMultiplier, atrPeriod, minTouches)");
        }

        int lookback = 250;
        int swing = 3;
        double clusterPercent = 0.8;
        double atrMultiplier = 0.75;
        int atrPeriod = 14;
        int minTouches = 2;

        if (parameters.Length >= 1)
        {
            lookback = Math.Max(50, Convert.ToInt32(parameters[0]));
        }

        if (parameters.Length >= 2)
        {
            swing = Math.Max(1, Convert.ToInt32(parameters[1]));
        }

        if (parameters.Length >= 3)
        {
            clusterPercent = Math.Max(0.05, Convert.ToDouble(parameters[2]));
        }

        if (parameters.Length >= 4)
        {
            atrMultiplier = Math.Max(0.1, Convert.ToDouble(parameters[3]));
        }

        if (parameters.Length >= 5)
        {
            atrPeriod = Math.Max(5, Convert.ToInt32(parameters[4]));
        }

        if (parameters.Length >= 6)
        {
            minTouches = Math.Max(1, Convert.ToInt32(parameters[5]));
        }

        lookback = Math.Max(lookback, swing * 4);

        return (lookback, swing, clusterPercent, atrMultiplier, atrPeriod, minTouches);
    }

    private static double[] ComputeAtrSeries(IReadOnlyList<Polygon.Client.Models.Bar> bars, int period)
    {
        var trueRanges = new double[bars.Count];
        trueRanges[0] = Math.Max(Epsilon, bars[0].High - bars[0].Low);

        for (int i = 1; i < bars.Count; i++)
        {
            var bar = bars[i];
            double prevClose = bars[i - 1].Close;
            double highLow = bar.High - bar.Low;
            double highClose = Math.Abs(bar.High - prevClose);
            double lowClose = Math.Abs(bar.Low - prevClose);
            trueRanges[i] = Math.Max(highLow, Math.Max(highClose, lowClose));
        }

        var atr = new double[bars.Count];
        double rolling = 0.0;
        period = Math.Max(2, period);

        for (int i = 0; i < bars.Count; i++)
        {
            rolling += trueRanges[i];
            if (i >= period)
            {
                rolling -= trueRanges[i - period];
            }

            var divisor = Math.Min(period, i + 1);
            atr[i] = rolling / divisor;
        }

        return atr;
    }

    private static (double High, double Low, double AvgVolume) ComputeWindowStats(IReadOnlyList<Polygon.Client.Models.Bar> bars, int start, int end)
    {
        double high = double.MinValue;
        double low = double.MaxValue;
        double volumeSum = 0.0;
        int count = end - start + 1;

        for (int i = start; i <= end; i++)
        {
            var bar = bars[i];
            high = Math.Max(high, bar.High);
            low = Math.Min(low, bar.Low);
            volumeSum += Math.Max(0.0, Convert.ToDouble(bar.Volume));
        }

        return (high, low, volumeSum / Math.Max(1, count));
    }

    private static PivotLevels? ComputePivotLevels(IReadOnlyList<Polygon.Client.Models.Bar> bars, int start, int end)
    {
        if (start < 0)
        {
            start = 0;
        }

        if (end - start < 2)
        {
            return null;
        }

        double high = double.MinValue;
        double low = double.MaxValue;

        for (int i = start; i <= end; i++)
        {
            high = Math.Max(high, bars[i].High);
            low = Math.Min(low, bars[i].Low);
        }

        double close = bars[end].Close;
        double pivot = (high + low + close) / 3.0;
        double r1 = 2 * pivot - low;
        double s1 = 2 * pivot - high;
        double r2 = pivot + (high - low);
        double s2 = pivot - (high - low);

        return new PivotLevels(pivot, s1, s2, r1, r2);
    }

    private static List<double> BuildConfluenceLevels(double windowHigh, double windowLow, PivotLevels? pivotLevels)
    {
        var levels = new List<double> { windowHigh, windowLow, (windowHigh + windowLow) / 2.0 };

        if (pivotLevels is { } pivots)
        {
            levels.Add(pivots.Pivot);
            levels.Add(pivots.S1);
            levels.Add(pivots.S2);
            levels.Add(pivots.R1);
            levels.Add(pivots.R2);
        }

        return levels;
    }

    private static void AddRollingExtremeCandidates(
        List<LevelCandidate> supports,
        List<LevelCandidate> resistances,
        double windowLow,
        double windowHigh,
        double tolerance,
        int index,
        long timestamp)
    {
        double width = Math.Max(tolerance, (windowHigh - windowLow) * 0.25);
        AddCandidate(supports, windowLow, 0.75, width, index, timestamp);
        AddCandidate(resistances, windowHigh, 0.75, width, index, timestamp);
    }

    private static void AddPivotCandidates(
        List<LevelCandidate> supports,
        List<LevelCandidate> resistances,
        PivotLevels? pivotLevels,
        double tolerance,
        int index,
        long timestamp,
        double close)
    {
        if (pivotLevels is not { } pivots)
        {
            return;
        }

        double width = tolerance * 1.2;
        AddCandidate(supports, pivots.S1, 0.9, width, index, timestamp);
        AddCandidate(supports, pivots.S2, 0.75, width * 1.1, index, timestamp);
        AddCandidate(resistances, pivots.R1, 0.9, width, index, timestamp);
        AddCandidate(resistances, pivots.R2, 0.75, width * 1.1, index, timestamp);

        if (pivots.Pivot <= close)
        {
            AddCandidate(supports, pivots.Pivot, 0.8, width, index, timestamp);
        }
        else
        {
            AddCandidate(resistances, pivots.Pivot, 0.8, width, index, timestamp);
        }
    }

    private static void GatherSwingCandidates(
        List<LevelCandidate> supports,
        List<LevelCandidate> resistances,
        IReadOnlyList<Polygon.Client.Models.Bar> bars,
        int windowStart,
        int currentIndex,
        int swing,
        double tolerance,
        double clusterFraction,
        double avgVolume,
        IReadOnlyList<double> confluenceLevels,
        int lookback,
        double[] atrSeries)
    {
        int pivotStart = windowStart + swing;
        int pivotEnd = currentIndex - swing;
        if (pivotEnd <= pivotStart)
        {
            return;
        }

        double lookbackAsDouble = Math.Max(lookback, swing * 4);

        for (int i = pivotStart; i <= pivotEnd; i++)
        {
            var bar = bars[i];
            bool isLowPivot = true;
            bool isHighPivot = true;

            for (int offset = 1; offset <= swing && (isLowPivot || isHighPivot); offset++)
            {
                var left = bars[i - offset];
                var right = bars[i + offset];

                if (left.Low <= bar.Low || right.Low < bar.Low)
                {
                    isLowPivot = false;
                }

                if (left.High >= bar.High || right.High > bar.High)
                {
                    isHighPivot = false;
                }
            }

            if (!isLowPivot && !isHighPivot)
            {
                continue;
            }

            double recency = 1.0 - (double)(currentIndex - i) / lookbackAsDouble;
            recency = Math.Clamp(recency, 0.05, 1.0);
            double volumeRatio = avgVolume <= 0 ? 1.0 : Math.Clamp(Convert.ToDouble(bar.Volume) / avgVolume, 0.2, 4.0);
            double atrLocal = atrSeries[i];
            double width = Math.Max(Math.Max(atrLocal, bar.High - bar.Low), tolerance);
            width = Math.Max(width, Math.Abs(bar.Close) * clusterFraction * 0.5);

            if (isLowPivot)
            {
                double weight = 1.0 + recency + 0.25 * volumeRatio + ComputeRoundNumberBoost(bar.Low) + GetConfluenceBoost(bar.Low, confluenceLevels, tolerance);
                AddCandidate(supports, bar.Low, weight, width, i, bar.Timestamp);
            }

            if (isHighPivot)
            {
                double weight = 1.0 + recency + 0.25 * volumeRatio + ComputeRoundNumberBoost(bar.High) + GetConfluenceBoost(bar.High, confluenceLevels, tolerance);
                AddCandidate(resistances, bar.High, weight, width, i, bar.Timestamp);
            }
        }
    }

    private static void AddVolumeProfileCandidates(
        List<LevelCandidate> supports,
        List<LevelCandidate> resistances,
        IReadOnlyList<Polygon.Client.Models.Bar> bars,
        int windowStart,
        int currentIndex,
        double tolerance,
        double avgVolume,
        double close)
    {
        var buckets = new Dictionary<int, VolumeBucket>();
        double bucketSize = Math.Max(tolerance * 0.75, Math.Abs(close) * 0.0025);

        for (int i = windowStart; i <= currentIndex; i++)
        {
            var bar = bars[i];
            double typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
            double volume = Math.Max(Epsilon, Convert.ToDouble(bar.Volume));
            int bucketKey = bucketSize <= Epsilon ? 0 : (int)Math.Round(typicalPrice / bucketSize);

            if (!buckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = new VolumeBucket();
                buckets[bucketKey] = bucket;
            }

            bucket.TotalVolume += volume;
            bucket.WeightedPrice += typicalPrice * volume;
            bucket.LastIndex = i;
            bucket.LastTimestamp = bar.Timestamp;
        }

        var topBuckets = buckets.Values
            .OrderByDescending(b => b.TotalVolume)
            .Take(4);

        foreach (var bucket in topBuckets)
        {
            if (bucket.TotalVolume <= 0)
            {
                continue;
            }

            double price = bucket.WeightedPrice / bucket.TotalVolume;
            double volumeFactor = avgVolume <= 0
                ? 1.0
                : Math.Clamp(bucket.TotalVolume / (avgVolume * Math.Max(1, currentIndex - windowStart + 1)), 0.4, 3.5);
            double weight = 0.6 + volumeFactor;
            double width = Math.Max(tolerance, bucketSize * 1.5);

            if (price <= close)
            {
                AddCandidate(supports, price, weight, width, bucket.LastIndex, bucket.LastTimestamp);
            }
            else
            {
                AddCandidate(resistances, price, weight, width, bucket.LastIndex, bucket.LastTimestamp);
            }
        }
    }

    private static void AddAnchoredVwapCandidate(
        List<LevelCandidate> supports,
        List<LevelCandidate> resistances,
        IReadOnlyList<Polygon.Client.Models.Bar> bars,
        int windowStart,
        int currentIndex,
        double tolerance,
        double close)
    {
        double vwap = ComputeAnchoredVwap(bars, windowStart, currentIndex);
        if (double.IsNaN(vwap))
        {
            return;
        }

        double width = Math.Max(tolerance, Math.Abs(close) * 0.002);
        double weight = 0.8;

        if (vwap <= close)
        {
            AddCandidate(supports, vwap, weight, width, currentIndex, bars[currentIndex].Timestamp);
        }
        else
        {
            AddCandidate(resistances, vwap, weight, width, currentIndex, bars[currentIndex].Timestamp);
        }
    }

    private static double ComputeAnchoredVwap(IReadOnlyList<Polygon.Client.Models.Bar> bars, int start, int end)
    {
        double pv = 0.0;
        double volumeSum = 0.0;

        for (int i = start; i <= end; i++)
        {
            var bar = bars[i];
            double typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
            double volume = Math.Max(Epsilon, Convert.ToDouble(bar.Volume));
            pv += typicalPrice * volume;
            volumeSum += volume;
        }

        return volumeSum <= 0 ? double.NaN : pv / volumeSum;
    }

    private static List<Zone> BuildZones(
        List<LevelCandidate> candidates,
        double tolerance,
        int minTouches,
        bool isSupport,
        int currentIndex,
        int windowStart)
    {
        if (candidates.Count == 0)
        {
            return new List<Zone>();
        }

        var zones = new List<Zone>();

        foreach (var candidate in candidates.OrderByDescending(c => c.Weight))
        {
            var zone = FindMatchingZone(zones, candidate, tolerance);
            if (zone is null)
            {
                zone = new Zone
                {
                    Center = candidate.Price,
                    Width = Math.Max(candidate.Width, tolerance),
                    LastIndex = candidate.Index,
                    LastTimestamp = candidate.Timestamp,
                    IsSupport = isSupport,
                    TouchCount = 0,
                    WeightSum = 0.0
                };
                zones.Add(zone);
            }

            double previousWeight = zone.WeightSum;
            zone.WeightSum += candidate.Weight;
            zone.TouchCount++;
            zone.Center = (zone.Center * previousWeight + candidate.Price * candidate.Weight) / zone.WeightSum;
            zone.Width = Math.Max(zone.Width, candidate.Width);
            zone.LastIndex = Math.Max(zone.LastIndex, candidate.Index);
            zone.LastTimestamp = Math.Max(zone.LastTimestamp, candidate.Timestamp);
        }

        int windowLength = currentIndex - windowStart + 1;

        foreach (var zone in zones)
        {
            double recency = 1.0 - (double)(currentIndex - zone.LastIndex) / Math.Max(1, windowLength);
            recency = Math.Clamp(recency, 0.05, 1.0);
            double touchBonus = zone.TouchCount >= minTouches
                ? 1.0 + 0.25 * (zone.TouchCount - minTouches + 1)
                : 0.75;
            zone.Strength = zone.WeightSum * touchBonus * (0.5 + recency);
        }

        return zones.OrderByDescending(z => z.Strength).ToList();
    }

    private static Zone? FindMatchingZone(List<Zone> zones, LevelCandidate candidate, double tolerance)
    {
        Zone? best = null;
        double minDistance = double.MaxValue;

        foreach (var zone in zones)
        {
            double threshold = Math.Max(tolerance, zone.Width);
            double distance = Math.Abs(zone.Center - candidate.Price);
            if (distance <= threshold && distance < minDistance)
            {
                best = zone;
                minDistance = distance;
            }
        }

        return best;
    }

    private static void ApplyBrokenPenalty(
        List<Zone> zones,
        IReadOnlyList<Polygon.Client.Models.Bar> bars,
        int windowStart,
        int windowEnd)
    {
        if (zones.Count == 0)
        {
            return;
        }

        int lookback = Math.Max(5, Math.Min(20, windowEnd - windowStart));
        int checkStart = Math.Max(windowStart, windowEnd - lookback);

        foreach (var zone in zones)
        {
            double penalty = 1.0;
            for (int i = checkStart; i <= windowEnd; i++)
            {
                var bar = bars[i];
                if (zone.IsSupport && bar.Close < zone.Center - zone.Width * 0.5)
                {
                    penalty = 0.6;
                    break;
                }

                if (!zone.IsSupport && bar.Close > zone.Center + zone.Width * 0.5)
                {
                    penalty = 0.6;
                    break;
                }
            }

            zone.Strength *= penalty;
        }
    }

    private static Zone? PickZone(List<Zone> zones, double close, bool isSupport)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        Zone? best = null;
        double bestScore = double.MinValue;

        foreach (var zone in zones)
        {
            double orientation = isSupport
                ? (zone.Center <= close + zone.Width * 0.25 ? 1.0 : 0.65)
                : (zone.Center >= close - zone.Width * 0.25 ? 1.0 : 0.65);

            double distance = Math.Abs(close - zone.Center);
            double closeness = 1.0 / (1.0 + distance);
            double score = zone.Strength * (0.7 + closeness) * orientation;

            if (score > bestScore)
            {
                bestScore = score;
                best = zone;
            }
        }

        return best;
    }

    private static SupportResistanceResult BuildResult(
        Polygon.Client.Models.Bar bar,
        Zone? supportZone,
        Zone? resistanceZone)
    {
        double close = bar.Close;

        double supportDistance = supportZone != null ? Math.Max(0.0, close - supportZone.Center) : double.NaN;
        double resistanceDistance = resistanceZone != null ? Math.Max(0.0, resistanceZone.Center - close) : double.NaN;

        double supportPct = double.IsNaN(supportDistance) ? double.NaN : supportDistance / Math.Max(Epsilon, Math.Abs(close)) * 100.0;
        double resistancePct = double.IsNaN(resistanceDistance) ? double.NaN : resistanceDistance / Math.Max(Epsilon, Math.Abs(close)) * 100.0;

        double value = 0.0;
        if (!double.IsNaN(supportPct) && !double.IsNaN(resistancePct))
        {
            value = resistancePct - supportPct;
        }
        else if (!double.IsNaN(supportPct))
        {
            value = -supportPct;
        }
        else if (!double.IsNaN(resistancePct))
        {
            value = resistancePct;
        }

        return new SupportResistanceResult
        {
            Timestamp = bar.Timestamp,
            Value = value,
            Support = supportZone?.Center ?? double.NaN,
            Resistance = resistanceZone?.Center ?? double.NaN,
            SupportStrength = supportZone != null ? NormalizeStrength(supportZone.Strength) : 0.0,
            ResistanceStrength = resistanceZone != null ? NormalizeStrength(resistanceZone.Strength) : 0.0,
            SupportZoneWidth = supportZone?.Width ?? double.NaN,
            ResistanceZoneWidth = resistanceZone?.Width ?? double.NaN,
            SupportDistance = supportDistance,
            ResistanceDistance = resistanceDistance,
            SupportDistancePercent = supportPct,
            ResistanceDistancePercent = resistancePct,
            SupportTouches = supportZone?.TouchCount ?? 0,
            ResistanceTouches = resistanceZone?.TouchCount ?? 0,
            SupportUpper = supportZone != null ? supportZone.Center + supportZone.Width / 2.0 : double.NaN,
            SupportLower = supportZone != null ? supportZone.Center - supportZone.Width / 2.0 : double.NaN,
            ResistanceUpper = resistanceZone != null ? resistanceZone.Center + resistanceZone.Width / 2.0 : double.NaN,
            ResistanceLower = resistanceZone != null ? resistanceZone.Center - resistanceZone.Width / 2.0 : double.NaN,
            NearSupport = supportZone != null && close <= supportZone.Center + supportZone.Width / 2.0 ? 1.0 : 0.0,
            NearResistance = resistanceZone != null && close >= resistanceZone.Center - resistanceZone.Width / 2.0 ? 1.0 : 0.0
        };
    }

    private static double NormalizeStrength(double strength)
    {
        return strength <= 0 ? 0.0 : 1.0 - Math.Exp(-strength / 3.0);
    }

    private static void AddCandidate(List<LevelCandidate> bucket, double price, double weight, double width, int index, long timestamp)
    {
        if (double.IsNaN(price) || double.IsInfinity(price))
        {
            return;
        }

        width = Math.Max(0.01, width);
        weight = Math.Max(0.05, weight);
        bucket.Add(new LevelCandidate(price, weight, width, index, timestamp));
    }

    private static double ComputeRoundNumberBoost(double price)
    {
        if (price <= 0)
        {
            return 0.0;
        }

        double tolerance = Math.Max(0.05, Math.Abs(price) * 0.002);
        double boost = 0.0;

        double whole = Math.Round(price);
        if (Math.Abs(price - whole) <= tolerance)
        {
            boost += 0.15;
        }

        double five = Math.Round(price / 5.0) * 5.0;
        if (Math.Abs(price - five) <= tolerance)
        {
            boost += 0.1;
        }

        double ten = Math.Round(price / 10.0) * 10.0;
        if (Math.Abs(price - ten) <= tolerance)
        {
            boost += 0.1;
        }

        return boost;
    }

    private static double GetConfluenceBoost(double price, IReadOnlyList<double> levels, double tolerance)
    {
        double boost = 0.0;

        foreach (var level in levels)
        {
            if (double.IsNaN(level) || double.IsInfinity(level))
            {
                continue;
            }

            double diff = Math.Abs(price - level);
            double threshold = tolerance * 1.5;
            if (diff <= threshold)
            {
                double closeness = 1.0 - diff / Math.Max(Epsilon, threshold);
                boost = Math.Max(boost, closeness * 0.6);
            }
        }

        return boost;
    }

    private sealed record LevelCandidate(
        double Price,
        double Weight,
        double Width,
        int Index,
        long Timestamp);

    private sealed class Zone
    {
        public double Center { get; set; }
        public double Width { get; set; }
        public int TouchCount { get; set; }
        public int LastIndex { get; set; }
        public long LastTimestamp { get; set; }
        public bool IsSupport { get; set; }
        public double WeightSum { get; set; }
        public double Strength { get; set; }
    }

    private sealed class VolumeBucket
    {
        public double TotalVolume { get; set; }
        public double WeightedPrice { get; set; }
        public int LastIndex { get; set; }
        public long LastTimestamp { get; set; }
    }

    private readonly record struct PivotLevels(
        double Pivot,
        double S1,
        double S2,
        double R1,
        double R2);
}
