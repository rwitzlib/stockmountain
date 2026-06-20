using Npgsql;

namespace StockMountain.MarketData.Catalog.Postgres;

public static class PostgresDataSourceFactory
{
    public static NpgsqlDataSource Create(string connectionStringOrUri)
    {
        var connectionString = NormalizeConnectionString(connectionStringOrUri);
        return NpgsqlDataSource.Create(connectionString);
    }

    public static string NormalizeConnectionString(string connectionStringOrUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringOrUri);

        var trimmed = connectionStringOrUri.Trim().Trim('"', '\'');

        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return ParsePostgresUri(trimmed);
        }

        var builder = new NpgsqlConnectionStringBuilder(trimmed);
        ApplyRailwaySslDefaults(builder);
        return builder.ConnectionString;
    }

    private static string ParsePostgresUri(string uriValue)
    {
        if (!Uri.TryCreate(uriValue, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("DATABASE_URL is not a valid PostgreSQL URI.", nameof(uriValue));
        }

        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException("DATABASE_URL URI must include a database name.", nameof(uriValue));
        }

        var (username, password) = ParseUserInfo(uri.UserInfo);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
        };

        ApplyQueryParameters(uri, builder);
        ApplyRailwaySslDefaults(builder);
        return builder.ConnectionString;
    }

    private static (string Username, string Password) ParseUserInfo(string userInfo)
    {
        if (string.IsNullOrWhiteSpace(userInfo))
        {
            return (string.Empty, string.Empty);
        }

        var separatorIndex = userInfo.IndexOf(':');
        if (separatorIndex < 0)
        {
            return (Uri.UnescapeDataString(userInfo), string.Empty);
        }

        var username = Uri.UnescapeDataString(userInfo[..separatorIndex]);
        var password = Uri.UnescapeDataString(userInfo[(separatorIndex + 1)..]);
        return (username, password);
    }

    private static void ApplyQueryParameters(Uri uri, NpgsqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return;
        }

        var query = uri.Query.TrimStart('?');
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..separatorIndex]);
            var value = Uri.UnescapeDataString(segment[(separatorIndex + 1)..]);

            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    if (Enum.TryParse<SslMode>(value, ignoreCase: true, out var sslMode))
                    {
                        builder.SslMode = sslMode;
                    }

                    break;
            }
        }
    }

    private static void ApplyRailwaySslDefaults(NpgsqlConnectionStringBuilder builder)
    {
        if (builder.SslMode is SslMode.Disable or SslMode.Allow)
        {
            return;
        }

        if (builder.Host is not null
            && builder.Host.Contains(".railway.app", StringComparison.OrdinalIgnoreCase)
            && builder.SslMode == SslMode.Prefer)
        {
            builder.SslMode = SslMode.Require;
        }
    }
}
