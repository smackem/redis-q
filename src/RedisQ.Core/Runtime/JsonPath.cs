using Newtonsoft.Json.Linq;

namespace RedisQ.Core.Runtime;

public static class JsonPath
{
    public static Value Select(string json, string path)
    {
        var obj = JObject.Parse(json);
        var token = obj.SelectToken(path);
        return Convert(token);
    }

    private static Value Convert(JToken? token) =>
        token?.Type switch
        {
            JTokenType.Integer => IntegerValue.Of(token.ToObject<int>()),
            JTokenType.Boolean => BoolValue.Of(token.ToObject<bool>()),
            JTokenType.Float => new RealValue(token.ToObject<double>()),
            JTokenType.String => new StringValue(token.ToObject<string>() ?? string.Empty),
            JTokenType.Array => new ListValue(token.Select(Convert).ToArray()),
            JTokenType.Null or null => NullValue.Instance,
            _ => new StringValue(token.ToString()),
        };
}
