using System.Text.Json.Serialization;

namespace Optimus.Models;

/// <summary>
/// Represents the DynamoDB stream record format sent by EventBridge Pipes.
/// Used when stream_view_type is KEYS_ONLY.
/// </summary>
public class DynamoDbStreamRecord
{
    [JsonPropertyName("eventID")]
    public string EventId { get; set; }

    [JsonPropertyName("eventName")]
    public string EventName { get; set; }

    [JsonPropertyName("dynamodb")]
    public DynamoDbData DynamoDb { get; set; }
}

public class DynamoDbData
{
    [JsonPropertyName("Keys")]
    public DynamoDbKeys Keys { get; set; }
}

public class DynamoDbKeys
{
    public DynamoDbStringValue PK { get; set; }
    public DynamoDbStringValue SK { get; set; }
}

public class DynamoDbStringValue
{
    public string S { get; set; }
}
