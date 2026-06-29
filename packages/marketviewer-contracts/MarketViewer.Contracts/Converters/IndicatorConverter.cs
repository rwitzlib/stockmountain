using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Indicator;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Converters;

public class IndicatorConverter : JsonConverter<Indicator>
{
    public override Indicator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var document = JsonDocument.ParseValue(ref reader);
        var jsonElement = document.RootElement;

        var indicatorFields = new Indicator();

        var nameAndParamsAndSelector = jsonElement.GetString().Split('(');


        if (Enum.TryParse<StudyType>(nameAndParamsAndSelector[0], out var studyType))
        {
            indicatorFields.Type = studyType;
        }
        else
        {
            throw new JsonException($"Invalid study type: {nameAndParamsAndSelector[0]}");
        }

        var paramsAndSelector = nameAndParamsAndSelector[1].Split(')');
        indicatorFields.Parameters = !string.IsNullOrEmpty(paramsAndSelector[0]) ? paramsAndSelector[0].Split(',') : null;
        indicatorFields.Selector = !string.IsNullOrEmpty(paramsAndSelector[1]) ? paramsAndSelector[1] : null;

        return indicatorFields;
    }

    public override void Write(Utf8JsonWriter writer, Indicator value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteStringValue($"{value.Type}:{string.Join(',', value.Parameters)}");

        writer.WriteEndObject();
    }
}
