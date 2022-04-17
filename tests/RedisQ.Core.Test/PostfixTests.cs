using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class PostfixTests : TestBase
{
    [Fact]
    public async Task Subscript()
    {
        var value1 = await Interpret("[1, 2, 3][0]");
        Assert.Equal(IntegerValue.Of(1), value1);
        var value2 = await Interpret("[1, 2, 3][-1]");
        Assert.Equal(IntegerValue.Of(3), value2);
        var value3 = await Interpret(@"""abc""[0]");
        Assert.Equal(new CharValue('a'), value3);
        var value4 = await Interpret(@"""abc""[-1]");
        Assert.Equal(new CharValue('c'), value4);
    }
}