using System.Threading.Tasks;
using RedisQ.Core.Runtime;

namespace RedisQ.Core.Test;

public class TestBase
{
    private protected Expr Compile(string source) => new Compiler().Compile(source);

    private protected Task<Value> Eval(Expr expr)
    {
        var ctx = Context.Root(Helpers.DummyRedis, Helpers.DefaultFunctions);
        return expr.Evaluate(ctx);
    }
}
