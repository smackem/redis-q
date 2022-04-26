using System;
using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class CompositeValuesTests : TestBase
{
    [Fact]
    public async Task List()
    {
        const string source = "[123, 2.5, \"abc\"]";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<ListValue>(value);
        var values = (ListValue)value;
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public async Task ListIndex()
    {
        var value1 = await Interpret("[1, 2, 3][1]");
        Assert.Equal(IntegerValue.Of(2), value1);
        var value2 = await Interpret("[1, 2, 3][0]");
        Assert.Equal(IntegerValue.Of(1), value2);
        var value3 = await Interpret("[1, 2, 3][-1]");
        Assert.Equal(IntegerValue.Of(3), value3);
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret("[1, 2, 3][100]"));
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
        const string source = "(123, 2.5, \"abc\")";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<TupleValue>(value);
        var tuple = (TupleValue)value;
        Assert.Collection(tuple.Items,
            v => Assert.Equal(IntegerValue.Of(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public async Task TupleIndex()
    {
        var value1 = await Interpret("(1, 2)[0]");
        Assert.Equal(IntegerValue.Of(1), value1);
        var value2 = await Interpret("(1, 2)[1]");
        Assert.Equal(IntegerValue.Of(2), value2);
        var value3 = await Interpret("(1, 2)[-1]");
        Assert.Equal(IntegerValue.Of(2), value3);
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret("(1, 2)[100]"));
    }

    [Fact]
    public async Task TupleFields()
    {
        var value1 = await Interpret(@"(one: 1, two: 2).one");
        Assert.Equal(IntegerValue.Of(1), value1);
        var value2 = await Interpret(@"(one: 1, two: 2).two");
        Assert.Equal(IntegerValue.Of(2), value2);
        var value3 = await Interpret(@"(one: 1, 2).one");
        Assert.Equal(IntegerValue.Of(1), value3);
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"(1, 2).none"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"(one: 1, 2).two"));
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
