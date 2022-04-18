using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class LogicalOperatorsTests : TestBase
{
    [Fact]
    public async Task Or()
    {
        var value = await Interpret(@"1 == 2 || 4.0 < 10.0");
        Assert.Equal(BoolValue.True, value);
    }

    [Fact]
    public async Task OrChain()
    {
        var value1 = await Interpret(@"1 == 2 || false || 4.0 > 10.0 || true");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 == 2 || false || 4.0 > 10.0 || false");
        Assert.Equal(BoolValue.False, value2);
        var value3 = await Interpret(@"1 == 2 || false || 4.0 > 10.0 || true && 1 > 0");
        Assert.Equal(BoolValue.True, value3);
    }

    [Fact]
    public async Task And()
    {
        var value = await Interpret(@"1 == 1 && 4.0 < 10.0");
        Assert.Equal(BoolValue.True, value);
    }

    [Fact]
    public async Task AndChain()
    {
        var value1 = await Interpret(@"1 == 1 && true && 5.0 > 4.0");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 == 1 && false && 5.0 > 4.0");
        Assert.Equal(BoolValue.False, value2);
        var value3 = await Interpret(@"1 == 1 && true && (true || false)");
        Assert.Equal(BoolValue.True, value3);
    }

    [Fact]
    public async Task Ternary()
    {
        var value1 = await Interpret(@"1 == 1 ? -10 : 10");
        Assert.Equal(IntegerValue.Of(-10), value1);
        var value2 = await Interpret(@"1 >= 100 ? -10 : 10");
        Assert.Equal(IntegerValue.Of(10), value2);
    }

    [Fact]
    public async Task LogicalShortCircuit()
    {
        var value1 = await Interpret(@"true || throw 'X'");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"false && throw 'X'");
        Assert.Equal(BoolValue.False, value2);
    }
}
