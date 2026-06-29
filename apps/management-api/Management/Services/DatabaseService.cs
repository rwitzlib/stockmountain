using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Management.Configuration;
using Management.Enums;
using Management.Models;

namespace Management.Services;

public class DatabaseService(IAmazonDynamoDB dynamodbClient, AwsConfigs configuration, ILogger<DatabaseService> logger)
{
    public async Task SaveDeployRecord(DeployRecord record)
    {
        var request = new PutItemRequest
        {
            TableName = configuration.DeployTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = record.Id } },
                { "Environment", new AttributeValue { S = record.Environment } },
                { "Repository", new AttributeValue { S = record.Repository } },
                { "File", new AttributeValue { S = record.File } },
                { "Image", new AttributeValue { S = record.Image } },
                { "Status", new AttributeValue { S = record.Status.ToString() } },
                { "Type", new AttributeValue { S = record.Type.ToString() } },
                { "CreatedAt", new AttributeValue { S = record.CreatedAt } },
                { "CreatedBy", new AttributeValue { S = record.CreatedBy } }
            }
        };

        await dynamodbClient.PutItemAsync(request);

        logger.LogInformation("Deploy record saved for service {service}.", record.Repository);
    }

    public async Task<DeployRecord> GetDeployRecord(string id)
    {
        var request = new GetItemRequest
        {
            TableName = configuration.DeployTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = id } }
            }
        };
        var response = await dynamodbClient.GetItemAsync(request);

        if (response.Item == null)
        {
            return null;
        }

        logger.LogInformation("Deploy record saved for service {service}.", response.Item["Repository"].S);

        return new DeployRecord
        {
            Id = response.Item["Id"].S,
            Environment = response.Item["Environment"].S,
            Repository = response.Item["Repository"].S,
            File = response.Item["File"].S,
            Image = response.Item["Image"].S,
            Status = Enum.Parse<DeployStatus>(response.Item["Status"].S),
            Type = Enum.Parse<DeployType>(response.Item["Type"].S),
            CreatedAt = response.Item["CreatedAt"].S,
            CreatedBy = response.Item["CreatedBy"].S
        };
    }

    
}
