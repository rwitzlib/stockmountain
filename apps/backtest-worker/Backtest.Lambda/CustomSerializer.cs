namespace Backtest.Lambda;

public class CustomSerializer : Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer
{
    public CustomSerializer() : base(
        options =>
        {
            options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
        })
    {

    }
}