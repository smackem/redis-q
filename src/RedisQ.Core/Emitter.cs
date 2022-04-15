using System.Globalization;
using RedisQ.Core.Lang;
using RedisQ.Core.Runtime;

namespace RedisQ.Core;

public class Emitter : RedisQLBaseVisitor<Expr>
{
    public override Expr VisitNumber(RedisQLParser.NumberContext context)
    {
        Value value = context switch
        {
            var ctx when ctx.Integer() != null => new IntegerValue(ParseInteger(ctx.Integer().GetText())),
            var ctx when ctx.Real() != null => new RealValue(ParseReal(ctx.Real().GetText())),
            _ => throw new CompilationException($"unexpected number literal: {context.GetText()}"),
        };
        return new LiteralExpr(value);
    }

    public override Expr VisitPrimary(RedisQLParser.PrimaryContext context)
    {
        Value? value = context switch
        {
            var ctx when ctx.StringLiteral() != null => new StringValue(ParseString(ctx.StringLiteral().GetText())),
            var ctx when ctx.CharLiteral() != null => new CharValue(ParseChar(ctx.CharLiteral().GetText())),
            _ => null,
        };

        return value != null
            ? new LiteralExpr(value)
            : base.VisitPrimary(context);
    }

    public override Expr VisitFunctionInvocation(RedisQLParser.FunctionInvocationContext context)
    {
        var ident = context.Ident().GetText();
        var arguments = context.arguments()?.expr().Select(a => a.Accept(this));
        return new FunctionExpr(ident, arguments?.ToArray() ?? Array.Empty<Expr>());
    }

    public override Expr VisitTuple(RedisQLParser.TupleContext context)
    {
        var items = context.expr().Select(a => a.Accept(this));
        return new TupleExpr(items.ToArray());
    }

    public override Expr VisitMain(RedisQLParser.MainContext context) =>
        context.expr().Accept(this);

    protected override Expr AggregateResult(Expr aggregate, Expr nextResult)
    {
        return (aggregate, nextResult) switch
        {
            (not null, null) => aggregate,
            (null, not null) => nextResult,
            (_, _) => base.AggregateResult(aggregate!, nextResult!),
        };
    }

    private static string ParseString(string s) => s.Trim('\"');
    private static char ParseChar(string s) => s.Trim('\'')[0];
    private static int ParseInteger(string s) => int.Parse(s.Replace("_", string.Empty));
    private static double ParseReal(string s) => double.Parse(s.Replace("_", string.Empty), CultureInfo.InvariantCulture);
}
