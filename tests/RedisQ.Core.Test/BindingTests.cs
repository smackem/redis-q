using System.Reflection.Metadata;
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
        ctx.Bind("a", IntegerValue.Of(1));
        var expr = Compile(@"a");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(1), value);
    }

    [Fact]
    public async Task NestedBinding()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", IntegerValue.Of(1));
        ctx = Context.Inherit(ctx);
        ctx.Bind("b", IntegerValue.Of(2));
        ctx = Context.Inherit(ctx);
        ctx.Bind("c", IntegerValue.Of(3));
        var expr = Compile(@"a + b + c");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(6), value);
    }

    [Fact]
    public async Task NestedBindingWithOverride()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", IntegerValue.Of(1));
        ctx = Context.Inherit(ctx);
        ctx.Bind("b", IntegerValue.Of(2));
        ctx = Context.Inherit(ctx);
        ctx.Bind("c", IntegerValue.Of(3));
        ctx = Context.Inherit(ctx);
        ctx.Bind("a", IntegerValue.Of(6));
        var expr = Compile(@"a + b + c");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(11), value);
    }

    [Fact]
    public async Task ThrowsOnUnknownIdent()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        ctx.Bind("a", IntegerValue.Of(1));
        var expr = Compile(@"X");
        await Assert.ThrowsAsync<RuntimeException>(() => expr.Evaluate(ctx));
    }

    [Fact]
    public async Task TopLevelBindingClause()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        var expr = Compile(@"let x = 123");
        var value = await expr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(123), value);
        var resolvedValue = ctx.Resolve("x");
        Assert.Equal(IntegerValue.Of(123), resolvedValue);
    }

    [Fact]
    public async Task FunctionValueBindings()
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        var funcExpr = Compile(@"let add(a,b) = a + b");
        var value = await funcExpr.Evaluate(ctx);
        Assert.IsType<FunctionValue>(value);
        var evalExpr = Compile(@"add(1, 2)");
        var result = await evalExpr.Evaluate(ctx);
        Assert.Equal(IntegerValue.Of(3), result);
        var faultExpr = Compile(@"add(1)");
        await Assert.ThrowsAsync<RuntimeException>(() => faultExpr.Evaluate(ctx));
    }
}
