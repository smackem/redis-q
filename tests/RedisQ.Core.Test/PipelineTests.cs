using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class PipelineTests : TestBase
{
    [Fact]
    public async Task PipelineWithRhsExpr()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        var expr = Compile(@"1 |> { 2 + $ }");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(3), value);
    }

    [Fact]
    public async Task MultiPipelineWithRhsExpr()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        var expr = Compile(@"1 |> { 2 + $ } |> { 3 + $ }");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(6), value);
    }
}
