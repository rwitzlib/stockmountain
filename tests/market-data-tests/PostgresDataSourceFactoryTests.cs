using Npgsql;
using StockMountain.MarketData.Catalog.Postgres;

namespace StockMountain.MarketData.Tests;

public class PostgresDataSourceFactoryTests
{
    [Fact]
    public void NormalizeConnectionString_ConvertsRailwayStyleUri()
    {
        var connectionString = PostgresDataSourceFactory.NormalizeConnectionString(
            "postgresql://postgres:secret%40word@postgres-production-ed00d.up.railway.app:5432/railway");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("postgres-production-ed00d.up.railway.app", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("railway", builder.Database);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal("secret@word", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void NormalizeConnectionString_TrimsQuotesFromKeyValueConnectionString()
    {
        var connectionString = PostgresDataSourceFactory.NormalizeConnectionString(
            "\"Host=localhost;Port=5432;Database=stockmountain;Username=postgres;Password=postgres\"");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal("stockmountain", builder.Database);
    }
}
