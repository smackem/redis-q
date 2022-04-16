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
        Assert.IsType<EnumerableValue>(value);
        var values = await ((EnumerableValue)value).Collect();
        Assert.Collection(values,
            v => Assert.Equal(new CharValue('a'), v),
            v => Assert.Equal(new IntegerValue(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }

    [Fact]
    public async Task EmptyList()
    {
        const string source = "[]";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.IsType<EnumerableValue>(value);
        var values = await ((EnumerableValue)value).Collect();
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
        Assert.Collection(tuple.Values,
            v => Assert.Equal(new CharValue('a'), v),
            v => Assert.Equal(new IntegerValue(123), v),
            v => Assert.Equal(new RealValue(2.5), v),
            v => Assert.Equal(new StringValue("abc"), v));
    }
}
