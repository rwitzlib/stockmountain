using System.Text.Json.Serialization;

namespace Management.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<DeployStatus>))]
public enum DeployStatus
{
    Success,
    InProgress,
    Failed
}
