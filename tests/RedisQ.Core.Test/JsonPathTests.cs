using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class JsonPathTests
{
    [Fact]
    public void ParseSimpleObject()
    {
        const string json = @"
{
    ""name"": ""bob"",
    ""age"": 123
}
";
        var value = JsonPath.Parse(json);
        Assert.Equal(
            TupleValue.Of(
                ("name", new StringValue("bob")),
                ("age", IntegerValue.Of(123))),
            value);
    }

    [Fact]
    public void ParseEmptyObject()
    {
        const string json = @"{}";
        var value = JsonPath.Parse(json);
        Assert.Equal(TupleValue.Empty, value);
    }

    [Fact]
    public void ParseArray()
    {
        const string json = @"
{
    ""array"": [4,8,15,16,23,42]
}";
        var value = JsonPath.Parse(json);
        Assert.Equal(
            TupleValue.Of(("array", Helpers.IntegerList(4, 8, 15, 16, 23, 42))),
            value);
    }
}
