namespace StockMountain.MarketData.Catalog.Postgres;

public static class BarSeriesCatalogSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS bar_series_files (
            object_key TEXT PRIMARY KEY,
            symbol TEXT NOT NULL,
            timeframe_multiplier INT NOT NULL,
            timeframe_unit TEXT NOT NULL,
            adjustment_policy TEXT NOT NULL,
            period_start TIMESTAMPTZ NOT NULL,
            period_end TIMESTAMPTZ NOT NULL,
            earliest_bar_start TIMESTAMPTZ NOT NULL,
            latest_bar_start TIMESTAMPTZ NOT NULL,
            bar_count BIGINT,
            registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS idx_bar_series_files_series
            ON bar_series_files (symbol, timeframe_multiplier, timeframe_unit, adjustment_policy);

        CREATE INDEX IF NOT EXISTS idx_bar_series_files_period
            ON bar_series_files (symbol, timeframe_multiplier, timeframe_unit, adjustment_policy, period_start, period_end);

        CREATE INDEX IF NOT EXISTS idx_bar_series_files_bar_span
            ON bar_series_files (symbol, timeframe_multiplier, timeframe_unit, adjustment_policy, earliest_bar_start, latest_bar_start);
        """;
}
