using System;
using System.Threading.Tasks;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class IntegrationTests
{
    private static readonly IRedisConnection DummyRedis = new DummyRedisConnection();
    private static readonly FunctionRegistry EmptyFunctions = new FunctionRegistry();

    [Fact]
    public async Task Literals()
    {
        var compiler = new Compiler();
        var expr = compiler.Compile("1");
        var ctx = Context.Root(DummyRedis, EmptyFunctions);
        Assert.Equal(new IntegerValue(1), await expr.Evaluate(ctx));
    }
}
