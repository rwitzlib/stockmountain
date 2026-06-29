using System.Text.Json.Serialization;

namespace Management.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<DeployType>))]
public enum DeployType
{
    Start,
    Stop
}
