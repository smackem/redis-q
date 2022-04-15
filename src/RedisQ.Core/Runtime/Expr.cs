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

public class TupleExpr : Expr
{
    public TupleExpr(IReadOnlyList<Expr> items) => Items = items;
    
    public IReadOnlyList<Expr> Items { get; }

    public override async Task<Value> Evaluate(Context ctx)
    {
        var values = new List<Value>();
        foreach (var expr in Items)
        {
            var value = await expr.Evaluate(ctx).ConfigureAwait(false);
            values.Add(value);
        }
        return new TupleValue(values);
    }
}

public class IdentExpr : Expr
{
    public IdentExpr(string ident) => Ident = ident;

    public string Ident { get; }

    public override Task<Value> Evaluate(Context ctx) =>
        Task.FromResult(ctx.LookupBinding(Ident) ?? throw new RuntimeException($"identifier {Ident} not found"));
}

public class FunctionExpr : Expr
{
    public FunctionExpr(string ident, IReadOnlyList<Expr> arguments) =>
        (Ident, Arguments) = (ident, arguments);

    public string Ident { get; }
    public IReadOnlyList<Expr> Arguments { get; }

    public override async Task<Value> Evaluate(Context ctx)
    {
        var function = ctx.Functions.Lookup(Ident, Arguments.Count);
        var arguments = new List<Value>();
        foreach (var arg in Arguments)
        {
            var value = await arg.Evaluate(ctx).ConfigureAwait(false);
            arguments.Add(value);
        }

        return await function.Invoke(ctx, arguments.ToArray()).ConfigureAwait(false);
    }
}
