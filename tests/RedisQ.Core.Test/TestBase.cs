using System.Threading.Tasks;
using RedisQ.Core.Runtime;

namespace RedisQ.Core.Test;

public class TestBase
{
    private protected static Expr Compile(string source) => new Compiler().Compile(source);

    private protected static Task<Value> Eval(Expr expr)
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        return expr.Evaluate(ctx);
    }

    private protected static Task<Value> Interpret(string source)
    {
        var expr = Compile(source);
        return Eval(expr);
    }
}
