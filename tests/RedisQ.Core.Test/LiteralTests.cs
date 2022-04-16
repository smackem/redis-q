using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class LiteralTests : TestBase
{
    [Fact]
    public async Task SingleInteger()
    {
        const string source = "1";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.Equal(IntegerValue.Of(1), value);
    }

    [Fact]
    public async Task SingleLongInteger()
    {
        const string source = "1_000_000_000";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.Equal(IntegerValue.Of(1_000_000_000), value);
    }

    [Fact]
    public async Task SingleString()
    {
        const string source = "\"hello\"";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.Equal(new StringValue("hello"), value);
    }

    [Fact]
    public async Task SingleChar()
    {
        const string source = "'a'";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.Equal(new CharValue('a'), value);
    }

    [Fact]
    public async Task SingleReal()
    {
        const string source = "1_234.25";
        var expr = Compile(source);
        var value = await Eval(expr);
        Assert.Equal(new RealValue(1_234.25), value);
    }
}
