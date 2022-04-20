using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class CompositeValuesTests : TestBase
{
    [Fact]
    public async Task List()
    {
        const string source = "['a', 123, 2.5, \"abc\"]";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<ListValue>(value);
        var values = (ListValue)value;
        Assert.Collection(values,
            v => Assert.Equal(new CharValue('a'), v),
            v => Assert.Equal(IntegerValue.Of(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public async Task EmptyList()
    {
        const string source = "[]";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<ListValue>(value);
        var values = (ListValue)value;
        Assert.Empty(values);
    }

    [Fact]
    public async Task Tuple()
    {
        const string source = "('a', 123, 2.5, \"abc\")";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<TupleValue>(value);
        var tuple = (TupleValue)value;
        Assert.Collection(tuple.Items,
            v => Assert.Equal(new CharValue('a'), v),
            v => Assert.Equal(IntegerValue.Of(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public async Task Range()
    {
        const string source = "1 .. 3";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<RangeValue>(value);
        var values = await ((RangeValue)value).Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }

    [Fact]
    public async Task EmptyRange()
    {
        const string source = "1 .. 0";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<RangeValue>(value);
        var values = await ((RangeValue)value).Collect();
        Assert.Empty(values);
    }
}
