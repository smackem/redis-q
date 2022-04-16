using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class BindingTests : TestBase
{
    [Fact]
    public async Task SimpleBinding()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", new IntegerValue(1));
        var expr = Compile(@"a");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(new IntegerValue(1), value);
    }

    [Fact]
    public async Task NestedBinding()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", new IntegerValue(1));
        ctx = Context.Inherit(ctx);
        ctx.Bind("b", new IntegerValue(2));
        ctx = Context.Inherit(ctx);
        ctx.Bind("c", new IntegerValue(3));
        var expr = Compile(@"a + b + c");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(new IntegerValue(6), value);
    }

    [Fact]
    public async Task NestedBindingWithOverride()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", new IntegerValue(1));
        ctx = Context.Inherit(ctx);
        ctx.Bind("b", new IntegerValue(2));
        ctx = Context.Inherit(ctx);
        ctx.Bind("c", new IntegerValue(3));
        ctx = Context.Inherit(ctx);
        ctx.Bind("a", new IntegerValue(6));
        var expr = Compile(@"a + b + c");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(new IntegerValue(11), value);
    }

    [Fact]
    public async Task ThrowsOnUnknownIdent()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", new IntegerValue(1));
        var expr = Compile(@"X");
        await Assert.ThrowsAsync<RuntimeException>(() => expr.Evaluate(ctx));
    }

    [Fact]
    public async Task TopLevelBindingClause()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        var expr = Compile(@"let x = 123");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(new IntegerValue(123), value);
        var resolvedValue = ctx.Resolve("x");
        Assert.Equal(new IntegerValue(123), resolvedValue);
    }
}
