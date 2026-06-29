using MarketViewer.Contracts.Models.Indicator;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Converters;

public class IndicatorPointConverter : JsonConverter<IndicatorPoint>
{
    public override IndicatorPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var document = JsonDocument.ParseValue(ref reader);
        var jsonElement = document.RootElement;

        // Check if it's MacdData by looking for Histogram or Signal properties
        if (jsonElement.TryGetProperty("Histogram", out _) || jsonElement.TryGetProperty("Signal", out _))
        {
            return JsonSerializer.Deserialize<MacdPoint>(jsonElement.GetRawText(), options);
        }

        // Check if it's RsiData by looking for Upper or Lower properties
        if (jsonElement.TryGetProperty("Upper", out _) || jsonElement.TryGetProperty("Lower", out _))
        {
            return JsonSerializer.Deserialize<RsiPoint>(jsonElement.GetRawText(), options);
        }

        // Check if it's Support/Resistance data by looking for Support or Resistance properties
        if (jsonElement.TryGetProperty("Support", out _) || jsonElement.TryGetProperty("Resistance", out _))
        {
            return JsonSerializer.Deserialize<SupportResistancePoint>(jsonElement.GetRawText(), options);
        }

        // Default to StudyData
        return JsonSerializer.Deserialize<IndicatorPoint>(jsonElement.GetRawText(), options);
    }

    public override void Write(Utf8JsonWriter writer, IndicatorPoint value, JsonSerializerOptions options)
    {
        // Create a new options instance without this converter to avoid infinite recursion
        var optionsWithoutConverter = new JsonSerializerOptions();
        foreach (var converter in options.Converters)
        {
            if (converter is not IndicatorPointConverter)
            {
                optionsWithoutConverter.Converters.Add(converter);
            }
        }

        // Copy other options
        optionsWithoutConverter.DefaultIgnoreCondition = options.DefaultIgnoreCondition;
        optionsWithoutConverter.NumberHandling = options.NumberHandling;
        optionsWithoutConverter.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Write the actual runtime type
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }
}
