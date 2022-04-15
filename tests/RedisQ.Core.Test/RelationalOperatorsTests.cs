using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class RelationalOperatorsTests : TestBase
{
    [Fact]
    public async Task Eq()
    {
        var value1 = await Interpret(@"1 == 1");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 == 2");
        Assert.Equal(BoolValue.False, value2);
        var value3 = await Interpret(@"'a' == 'a'");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret(@"'a' == 'A'");
        Assert.Equal(BoolValue.False, value4);
        var value5 = await Interpret(@"12.5 == 12.5");
        Assert.Equal(BoolValue.True, value5);
        var value6 = await Interpret(@"12 == 12.0");
        Assert.Equal(BoolValue.False, value6);
        var value7 = await Interpret(@"""abc"" == ""abc""");
        Assert.Equal(BoolValue.True, value7);
        var value8 = await Interpret(@"true == true");
        Assert.Equal(BoolValue.True, value8);
        var value9 = await Interpret(@"true == false");
        Assert.Equal(BoolValue.False, value9);
        var value10 = await Interpret(@"1 == ""1""");
        Assert.Equal(BoolValue.False, value10);
        var value11 = await Interpret(@"null == null");
        Assert.Equal(BoolValue.True, value11);
        var value12 = await Interpret(@"null == 1");
        Assert.Equal(BoolValue.False, value12);
    }

    [Fact]
    public async Task Ne()
    {
        var value1 = await Interpret(@"1 != 1");
        Assert.NotEqual(BoolValue.True, value1);
        var value2 = await Interpret(@"1 != 2");
        Assert.NotEqual(BoolValue.False, value2);
        var value3 = await Interpret(@"'a' != 'a'");
        Assert.NotEqual(BoolValue.True, value3);
        var value4 = await Interpret(@"'a' != 'A'");
        Assert.NotEqual(BoolValue.False, value4);
        var value5 = await Interpret(@"12.5 != 12.5");
        Assert.NotEqual(BoolValue.True, value5);
        var value6 = await Interpret(@"12 != 12.0");
        Assert.NotEqual(BoolValue.False, value6);
        var value7 = await Interpret(@"""abc"" != ""abc""");
        Assert.NotEqual(BoolValue.True, value7);
        var value8 = await Interpret(@"true != true");
        Assert.NotEqual(BoolValue.True, value8);
        var value9 = await Interpret(@"true != false");
        Assert.NotEqual(BoolValue.False, value9);
        var value10 = await Interpret(@"1 != ""1""");
        Assert.NotEqual(BoolValue.False, value10);
    }

    [Fact]
    public async Task Lt()
    {
        var value1 = await Interpret(@"1 < 2");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 < 1");
        Assert.Equal(BoolValue.False, value2);
        var value3 = await Interpret(@"1 < 2.0");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret(@"""abc"" < ""def""");
        Assert.Equal(BoolValue.True, value4);
    }

    [Fact]
    public async Task Le()
    {
        var value1 = await Interpret(@"1 <= 2");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 <= 1");
        Assert.Equal(BoolValue.True, value2);
        var value3 = await Interpret(@"1 <= 2.0");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret(@"""abc"" <= ""def""");
        Assert.Equal(BoolValue.True, value4);
        var value5 = await Interpret(@"1 <= 0");
        Assert.Equal(BoolValue.False, value5);
    }

    [Fact]
    public async Task Gt()
    {
        var value1 = await Interpret(@"2 > 1");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 > 1");
        Assert.Equal(BoolValue.False, value2);
        var value3 = await Interpret(@"2.0 > 1");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret(@"""def"" > ""abc""");
        Assert.Equal(BoolValue.True, value4);
    }

    [Fact]
    public async Task Ge()
    {
        var value1 = await Interpret(@"2 >= 1");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"1 >= 1");
        Assert.Equal(BoolValue.True, value2);
        var value3 = await Interpret(@"2.0 >= 1");
        Assert.Equal(BoolValue.True, value3);
        var value4 = await Interpret(@"""def"" >= ""abc""");
        Assert.Equal(BoolValue.True, value4);
        var value5 = await Interpret(@"0 >= 1");
        Assert.Equal(BoolValue.False, value5);
    }

    [Fact]
    public async Task Match()
    {
        var value1 = await Interpret(@"""abc"" ~= ""[a-c]+""");
        Assert.Equal(BoolValue.True, value1);
        var value2 = await Interpret(@"""abc"" ~= ""[X]{2}""");
        Assert.Equal(BoolValue.False, value2);
    }

    [Fact]
    public async Task ThrowsOnIncompatibleRelationalOperands()
    {
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"1 < []"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"(1,2) < []"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"['a'] >= 'x'"));
    }
}
