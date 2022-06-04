using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class ProgramTests : TestBase
{
    [Fact]
    public async Task TestProgram()
    {
        var value = await Interpret("1; 1+1; let x = 3 in x");
        Assert.IsType<ListValue>(value);
        var coll = (ListValue)value;
        Assert.Collection(coll,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }
}
