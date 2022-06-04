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
    public void ParseEmptyList()
    {
        const string json = @"[]";
        var value = JsonPath.Parse(json);
        Assert.Equal(ListValue.Empty, value);
    }

    [Fact]
    public void ParseList()
    {
        const string json = @"[42, ""abc""]";
        var value = JsonPath.Parse(json);
        Assert.IsType<ListValue>(value);
        Assert.Collection((ListValue) value,
            v => Assert.Equal(IntegerValue.Of(42), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public void ParseScalar()
    {
        const string json = @"42";
        var value = JsonPath.Parse(json);
        Assert.Equal(IntegerValue.Of(42), value);
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

    [Fact]
    public void ListToJson()
    {
        var list = Helpers.IntegerList(1, 2, 3, 4, 100);
        var json = JsonPath.ToJson(list);
        Assert.Equal(list, JsonPath.Parse(json));
    }

    [Fact]
    public void TupleToJson()
    {
        var obj = TupleValue.Of(
            ("a", IntegerValue.Of(1)),
            ("b", new StringValue("abc")));
        var json = JsonPath.ToJson(obj);
        Assert.Equal(obj, JsonPath.Parse(json));
    }
}
