using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class MultiplicativeOperatorsTests : TestBase
{
    [Fact]
    public async Task MultiplyIntegers()
    {
        var value = await Interpret(@"2 * 3");
        Assert.Equal(IntegerValue.Of(6), value);
    }

    [Fact]
    public async Task MultiplyIntegerAndReal()
    {
        var value1 = await Interpret(@"2 * 3.0");
        Assert.Equal(new RealValue(6.0), value1);
        var value2 = await Interpret(@"3.0 * 2");
        Assert.Equal(new RealValue(6.0), value2);
    }

    [Fact]
    public async Task MultiplyReals()
    {
        var value = await Interpret(@"2.0 * 3.0");
        Assert.Equal(new RealValue(6.0), value);
    }

    [Fact]
    public async Task DivideIntegers()
    {
        var value = await Interpret(@"10 / 3");
        Assert.Equal(IntegerValue.Of(3), value);
    }

    [Fact]
    public async Task DivideIntegerAndReal()
    {
        var value = await Interpret(@"10 / 2.5");
        Assert.Equal(new RealValue(4.0), value);
    }

    [Fact]
    public async Task DivideReals()
    {
        var value = await Interpret(@"10.0 / 2.5");
        Assert.Equal(new RealValue(4.0), value);
    }

    [Fact]
    public async Task LongMultiplicative()
    {
        var value = await Interpret(@"(2 * 3 * 5) / 10.0");
        Assert.Equal(new RealValue(3.0), value);
    }
}
