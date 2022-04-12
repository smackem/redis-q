namespace RedisQ.Core.Runtime;

public abstract class Expr
{
    public abstract Task<Value> Evaluate(Context ctx);
}

public class LiteralExpr : Expr
{
    public LiteralExpr(Value literal) => Literal = literal;

    public Value Literal { get; }

    public override Task<Value> Evaluate(Context _) => Task.FromResult(Literal);
}

public class FunctionExpr : Expr
{
    public FunctionExpr(FunctionDefinition function, IReadOnlyList<Expr> arguments) =>
        (Function, Arguments) = (function, arguments);

    public FunctionDefinition Function { get; }
    public IReadOnlyList<Expr> Arguments { get; }

    public override async Task<Value> Evaluate(Context ctx)
    {
        var arguments = new List<Value>();
        foreach (var arg in Arguments)
        {
            arguments.Add(await arg.Evaluate(ctx).ConfigureAwait(false));
        }

        return await Function.Invoke(ctx, arguments.ToArray()).ConfigureAwait(false);
    }
}
