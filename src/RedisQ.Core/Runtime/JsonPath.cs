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

    /// <summary>
    /// parses a JSON value into a <see cref="Value"/>.
    /// </summary>
    public static Value Parse(string json) =>
        Convert(JToken.Parse(json));

    public static string ToJson(Value value) =>
        value switch
        {
            IntegerValue or RealValue => value.AsString(),
            StringValue s => '"' + s.Value + '"',
            TimestampValue
                or DurationValue
                or RedisValue
                or RedisKeyValue => '"' + value.AsString() + '"',
            ListValue list => '[' + string.Join(", ", list.Select(ToJson)) + ']',
            TupleValue tuple => Convert(tuple),
            NullValue => "null",
            BoolValue b => b.Value ? "true" : "false",
            _ => throw new ArgumentException($"cannot convert to json: {value}"),
        };

    private static string Convert(TupleValue tuple)
    {
        var items = tuple.Items.Select((v, i) =>
            i < 0 || i >= tuple.FieldNames.Count || string.IsNullOrEmpty(tuple.FieldNames[i])
                ? $"\"item{i}\": {ToJson(v)}"
                : $"\"{tuple.FieldNames[i]}\": {ToJson(v)}");
        return '{' + string.Join(", ", items) + '}';
    }

    private static Value Convert(JToken? token) =>
        token?.Type switch
        {
            JTokenType.Integer => IntegerValue.Of(token.ToObject<int>()),
            JTokenType.Boolean => BoolValue.Of(token.ToObject<bool>()),
            JTokenType.Float => new RealValue(token.ToObject<double>()),
            JTokenType.String => new StringValue(token.ToObject<string>() ?? string.Empty),
            JTokenType.Array => new ListValue(token.Select(Convert).ToArray()),
            JTokenType.Object => ConvertObject((JObject) token),
            JTokenType.Null or null => NullValue.Instance,
            _ => new StringValue(token.ToString()),
        };

    private static TupleValue ConvertObject(JObject obj)
    {
        var nameValuePairs = obj.Properties()
            .Select(prop => (prop.Name, Convert(prop.Value)))
            .ToArray();
        return TupleValue.Of(nameValuePairs);
    }
}
