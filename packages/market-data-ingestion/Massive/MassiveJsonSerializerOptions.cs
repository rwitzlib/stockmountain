using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockMountain.MarketData.Ingestion.Massive;

internal static class MassiveJsonSerializerOptions
{
    public static JsonSerializerOptions Create() =>
        new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters =
            {
                new FlexibleInt64JsonConverter(),
                new FlexibleInt32JsonConverter(),
            },
        };
}

internal sealed class FlexibleInt64JsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value,
            JsonTokenType.Number => Convert.ToInt64(reader.GetDouble()),
            JsonTokenType.String when long.TryParse(reader.GetString(), out var parsed) => parsed,
            _ => throw new JsonException($"Cannot convert JSON token '{reader.TokenType}' to Int64."),
        };

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

internal sealed class FlexibleInt32JsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value,
            JsonTokenType.Number => Convert.ToInt32(reader.GetDouble()),
            JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
            _ => throw new JsonException($"Cannot convert JSON token '{reader.TokenType}' to Int32."),
        };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
