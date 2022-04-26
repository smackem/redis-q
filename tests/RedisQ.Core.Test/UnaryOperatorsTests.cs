using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class UnaryOperatorsTests : TestBase
{
    [Fact]
    public async Task Negate()
    {
        var value1 = await Interpret("-(123)");
        Assert.Equal(IntegerValue.Of(-123), value1);
        var value2 = await Interpret("-(1.5 - 1)");
        Assert.Equal(new RealValue(-0.5), value2);
        var value3 = await Interpret("-(123) == -123");
        Assert.Equal(BoolValue.True, value3);
    }

    [Fact]
    public async Task Positive()
    {
        var value1 = await Interpret("+(123)");
        Assert.Equal(IntegerValue.Of(123), value1);
        var value2 = await Interpret("+(1.5 - 1)");
        Assert.Equal(new RealValue(0.5), value2);
        var value3 = await Interpret("+(123) == +123");
        Assert.Equal(BoolValue.True, value3);
    }

    [Fact]
    public async Task LogicalNot()
    {
        var value1 = await Interpret("!1");
        Assert.Equal(BoolValue.False, value1);
        var value2 = await Interpret("!false");
        Assert.Equal(BoolValue.True, value2);
        var value3 = await Interpret("!\"\"");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret("!\"abc\"");
        Assert.Equal(BoolValue.False, value4);
        var value5 = await Interpret("!!!false");
        Assert.Equal(BoolValue.True, value5);
        var value6 = await Interpret("![]");
        Assert.Equal(BoolValue.True, value6);
    }

    [Fact]
    public async Task ThrowsOnIncompatibleOperand()
    {
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"-[]"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"+[]"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"-(1, 2)"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"+""abc"""));
    }
}
