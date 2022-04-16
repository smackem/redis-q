using System.Globalization;
using RedisQ.Core.Lang;
using RedisQ.Core.Runtime;

namespace RedisQ.Core;

public class Emitter : RedisQLBaseVisitor<Expr>
{
    public override Expr VisitMain(RedisQLParser.MainContext context) =>
        context.expr().Accept(this);

    public override Expr VisitFromExpr(RedisQLParser.FromExprContext context)
    {
        var head = (FromClause) context.fromClause().Accept(this);
        var selection = context.selectClause().conditionalOrExpr().Accept(this);
        var nested = context.nestedClause()
            .Select(clause => (NestedClause) clause.Accept(this))
            .ToArray();
        return new FromExpr(head, nested, selection);
    }

    public override Expr VisitFromClause(RedisQLParser.FromClauseContext context) =>
        new FromClause(context.Ident().GetText(), context.primary().Accept(this));

    public override Expr VisitLetClause(RedisQLParser.LetClauseContext context) =>
        new LetClause(context.Ident().GetText(), context.conditionalOrExpr().Accept(this));

    public override Expr VisitWhereClause(RedisQLParser.WhereClauseContext context) =>
        new WhereClause(context.conditionalOrExpr().Accept(this));

    public override Expr VisitConditionalOrExpr(RedisQLParser.ConditionalOrExprContext context)
    {
        if (context.conditionalOrExpr() == null) return base.VisitConditionalOrExpr(context);
        var left = context.conditionalAndExpr().Accept(this);
        var right = context.conditionalOrExpr().Accept(this);
        return new OrExpr(left, right);
    }

    public override Expr VisitConditionalAndExpr(RedisQLParser.ConditionalAndExprContext context)
    {
        if (context.conditionalAndExpr() == null) return base.VisitConditionalAndExpr(context);
        var left = context.relationalExpr().Accept(this);
        var right = context.conditionalAndExpr().Accept(this);
        return new AndExpr(left, right);
    }

    public override Expr VisitRelationalExpr(RedisQLParser.RelationalExprContext context)
    {
        if (context.additiveExpr().Length == 1) return base.VisitRelationalExpr(context);
        var left = context.additiveExpr()[0].Accept(this);
        var right = context.additiveExpr()[1].Accept(this);
        return context.relationalOp() switch
        {
            var op when op.Eq() != null => new EqExpr(left, right),
            var op when op.Ne() != null => new NeExpr(left, right),
            var op when op.Lt() != null => new LtExpr(left, right),
            var op when op.Le() != null => new LeExpr(left, right),
            var op when op.Gt() != null => new GtExpr(left, right),
            var op when op.Ge() != null => new GeExpr(left, right),
            var op when op.Match() != null => new MatchExpr(left, right),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitAdditiveExpr(RedisQLParser.AdditiveExprContext context)
    {
        if (context.additiveExpr() == null) return base.VisitAdditiveExpr(context);
        var left = context.additiveExpr().Accept(this);
        var right = context.multiplicativeExpr().Accept(this);
        return context.additiveOp() switch
        {
            var op when op.Plus() != null => new PlusExpr(left, right),
            var op when op.Minus() != null => new MinusExpr(left, right),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitMultiplicativeExpr(RedisQLParser.MultiplicativeExprContext context)
    {
        if (context.multiplicativeExpr() == null) return base.VisitMultiplicativeExpr(context);
        var left = context.multiplicativeExpr().Accept(this);
        var right = context.unaryExpr().Accept(this);
        return context.multiplicativeOp() switch
        {
            var op when op.Times() != null => new TimesExpr(left, right),
            var op when op.Div() != null => new DivExpr(left, right),
            var op when op.Mod() != null => new ModExpr(left, right),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitUnaryExpr(RedisQLParser.UnaryExprContext context)
    {
        if (context.unaryExpr() == null) return base.VisitUnaryExpr(context);
        var operand = context.unaryExpr().Accept(this);
        return context.unaryOp() switch
        {
            var op when op.Minus() != null => new NegExpr(operand),
            var op when op.Plus() != null => new PosExpr(operand),
            var op when op.Not() != null => new NotExpr(operand),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitPrimary(RedisQLParser.PrimaryContext context)
    {
        Value? value = context switch
        {
            var ctx when ctx.StringLiteral() != null => new StringValue(ParseString(ctx.StringLiteral().GetText())),
            var ctx when ctx.CharLiteral() != null => new CharValue(ParseChar(ctx.CharLiteral().GetText())),
            var ctx when ctx.True() != null => BoolValue.True,
            var ctx when ctx.False() != null => BoolValue.False,
            var ctx when ctx.Null() != null => NullValue.Instance,
            _ => null,
        };

        if (value != null) return new LiteralExpr(value);
        return context.Ident() != null
            ? new IdentExpr(context.Ident().GetText())
            : base.VisitPrimary(context);
    }

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

    public override Expr VisitList(RedisQLParser.ListContext context)
    {
        var items = context.arguments()?.expr().Select(a => a.Accept(this));
        return items != null
            ? new ListExpr(items.ToArray())
            : ListExpr.Empty;
    }

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
