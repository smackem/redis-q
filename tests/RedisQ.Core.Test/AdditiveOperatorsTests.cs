using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class AdditiveOperatorsTests : TestBase
{
    [Fact]
    public async Task AddIntegers()
    {
        var value = await Interpret(@"1 + 1");
        Assert.Equal(IntegerValue.Of(2), value);
    }

    [Fact]
    public async Task AddReals()
    {
        var value = await Interpret(@"1.0 + 2.5");
        Assert.Equal(new RealValue(3.5), value);
    }

    [Fact]
    public async Task AddIntegerAndReal()
    {
        var value1 = await Interpret(@"1 + 2.5");
        Assert.Equal(new RealValue(3.5), value1);
        var value2 = await Interpret(@"2.5 + 1");
        Assert.Equal(new RealValue(3.5), value2);
    }

    [Fact]
    public async Task AddIntegerAndChar()
    {
        var value1 = await Interpret(@"'a' + 1");
        Assert.Equal(IntegerValue.Of('a' + 1), value1);
        var value2 = await Interpret(@"1 + 'a'");
        Assert.Equal(IntegerValue.Of('a' + 1), value2);
    }

    [Fact]
    public async Task AddChars()
    {
        var value = await Interpret(@"'a' + 'b'");
        Assert.Equal(IntegerValue.Of('a' + 'b'), value);
    }
    
    [Fact]
    public async Task AddStrings()
    {
        var value = await Interpret(@"""abc"" + ""_"" + ""def""");
        Assert.Equal(new StringValue("abc_def"), value);
    }

    [Fact]
    public async Task AddStringAndOthers()
    {
        var value1 = await Interpret(@"""abc"" + 'X'");
        Assert.Equal(new StringValue("abcX"), value1);
        var value2 = await Interpret(@"""abc"" + 123");
        Assert.Equal(new StringValue("abc123"), value2);
        var value3 = await Interpret(@"""abc"" + 123.25");
        Assert.Equal(new StringValue("abc123.25"), value3);
        var value4 = await Interpret(@"'X' + ""_abc""");
        Assert.Equal(new StringValue("X_abc"), value4);
        var value5 = await Interpret(@"123 + ""abc""");
        Assert.Equal(new StringValue("123abc"), value5);
        var value6 = await Interpret(@"123.25 + ""abc""");
        Assert.Equal(new StringValue("123.25abc"), value6);
        var value7 = await Interpret(@"""abc"" + '_' + true");
        Assert.Equal(new StringValue("abc_True"), value7);
        var value8 = await Interpret(@"false + ('_' + ""abc"")");
        Assert.Equal(new StringValue("False_abc"), value8);
    }

    [Fact]
    public async Task SubtractIntegers()
    {
        var value = await Interpret(@"1 - 1");
        Assert.Equal(IntegerValue.Of(0), value);
    }

    [Fact]
    public async Task SubtractIntegerAndReal()
    {
        var value1 = await Interpret(@"1 - 1.0");
        Assert.Equal(new RealValue(0), value1);
        var value2 = await Interpret(@"1.0 - 1");
        Assert.Equal(new RealValue(0), value2);
    }

    [Fact]
    public async Task SubtractIntegerAndChar()
    {
        var value1 = await Interpret(@"'a' - 1");
        Assert.Equal(IntegerValue.Of('a' - 1), value1);
        var value2 = await Interpret(@"1 - 'a'");
        Assert.Equal(IntegerValue.Of(1 - 'a'), value2);
    }

    [Fact]
    public async Task LongAdditiveWithIntegers()
    {
        var value = await Interpret(@"1 + 10 - 5 + 4 - 9");
        Assert.Equal(IntegerValue.Of(1), value);
    }

    [Fact]
    public async Task LongAdditiveWithMixedTypes()
    {
        var value = await Interpret(@"(1 + 10.5 - 5.5 + 4 - 9) + ""X""");
        Assert.Equal(new StringValue("1X"), value);
    }

    [Fact]
    public async Task LongAdditiveWithNull()
    {
        var value = await Interpret(@"(1 + 10.5 - 5.5 + 4 - 9) + null");
        Assert.Equal(NullValue.Instance, value);
    }

    [Fact]
    public async Task ThrowsOnIncompatibleAdditiveOperands()
    {
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"(1, 2) + 1"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"'a' + 1.0"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"[1] + 2"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"2 + (1, 2)"));
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"[] + (1, 2)"));
    }
}
