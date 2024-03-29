using System.Globalization;
using Antlr4.Runtime.Tree;
using RedisQ.Core.Lang;
using RedisQ.Core.Runtime;

namespace RedisQ.Core;

internal class Emitter : RedisQLBaseVisitor<Expr>
{
    private const string PipelineValueIdent = "$";
    private int _nestingLevel;
    private readonly bool _parseIntAsReal;

    public Emitter(bool parseIntAsReal)
    {
        _parseIntAsReal = parseIntAsReal;
    }

    public override Expr VisitMain(RedisQLParser.MainContext context)
    {
        var expressions = context.mainExpr()
            .Select(expr => expr.Accept(this))
            .ToArray();
        return expressions.Length == 1
            ? expressions[0]
            : new ProgramExpr(expressions);
    }

    public override Expr VisitMainExpr(RedisQLParser.MainExprContext context) =>
        (context.letExpr() as IParseTree
         ?? context.letClause() as IParseTree
         ?? context.funcBinding()).Accept(this);

    public override Expr VisitLetExpr(RedisQLParser.LetExprContext context) =>
        context.letClause() != null
            ? new LetExpr((LetClause)context.letClause().Accept(this), context.letExpr().Accept(this))
            : context.pipelineExpr().Accept(this);

    public override Expr VisitPipelineExpr(RedisQLParser.PipelineExprContext context)
    {
        var pipelineExpr = context.pipelineExpr()?.Accept(this);
        if (pipelineExpr == null) return context.expr().Accept(this);

        if (context.functionInvocation() != null)
        {
            return EmitFunctionInvocation(context.functionInvocation(), pipelineExpr);
        }

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (context.pipelineRhsExpr() != null)
        {
            return new LetExpr(new LetClause(PipelineValueIdent, pipelineExpr), context.pipelineRhsExpr().Accept(this));
        }

        return context.expr().Accept(this);
    }

    public override Expr VisitFromExpr(RedisQLParser.FromExprContext context)
    {
        var head = (FromClause) context.fromClause().Accept(this);
        var selection = context.selectClause().Accept(this);
        var nested = context.nestedClause()
            .Select(clause => (NestedClause) clause.Accept(this))
            .ToArray();
        var @from = new FromExpr(head, nested, selection);
        return _nestingLevel > 0 ? new EagerFromExpr(@from) : @from;
    }

    public override Expr VisitSelectClause(RedisQLParser.SelectClauseContext context)
    {
        _nestingLevel++;
        var result = context.expr().Accept(this);
        _nestingLevel--;
        return result;
    }

    public override Expr VisitNestedClause(RedisQLParser.NestedClauseContext context)
    {
        _nestingLevel++;
        var result = base.VisitNestedClause(context);
        _nestingLevel--;
        return result;
    }

    public override Expr VisitFromClause(RedisQLParser.FromClauseContext context) =>
        new FromClause(context.Ident().GetText(), context.ternaryExpr().Accept(this));

    public override Expr VisitLetClause(RedisQLParser.LetClauseContext context) =>
        new LetClause(context.Ident().GetText(), context.pipelineExpr().Accept(this));

    public override Expr VisitWhereClause(RedisQLParser.WhereClauseContext context) =>
        new WhereClause(context.ternaryExpr().Accept(this));

    public override Expr VisitLimitClause(RedisQLParser.LimitClauseContext context) =>
        new LimitClause(
            context.limitClauseLimitPart()?.Accept(this),
            context.limitClauseOffsetPart()?.Accept(this));

    public override Expr VisitLimitClauseLimitPart(RedisQLParser.LimitClauseLimitPartContext context) =>
        context.ternaryExpr().Accept(this);

    public override Expr VisitLimitClauseOffsetPart(RedisQLParser.LimitClauseOffsetPartContext context) =>
        context.ternaryExpr().Accept(this);

    public override Expr VisitGroupByClause(RedisQLParser.GroupByClauseContext context) =>
        new GroupByClause(
            context.ternaryExpr(0).Accept(this),
            context.ternaryExpr(1).Accept(this),
            context.Ident().GetText());

    public override Expr VisitOrderByClause(RedisQLParser.OrderByClauseContext context) =>
        new OrderByClause(context.ternaryExpr().Accept(this), context.Descending() != null);

    public override Expr VisitTernaryExpr(RedisQLParser.TernaryExprContext context) =>
        context.ternaryExpr() != null
        ? new TernaryExpr(
            context.conditionalOrExpr(0).Accept(this),
            context.conditionalOrExpr(1).Accept(this),
            context.ternaryExpr().Accept(this))
        : context.conditionalOrExpr(0).Accept(this);

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
        if (context.compositionalExpr().Length == 1) return base.VisitRelationalExpr(context);
        var left = context.compositionalExpr(0).Accept(this);
        var right = context.compositionalExpr(1).Accept(this);
        return context.relationalOp() switch
        {
            var op when op.Eq() != null => new EqExpr(left, right),
            var op when op.Ne() != null => new NeExpr(left, right),
            var op when op.Lt() != null => new LtExpr(left, right),
            var op when op.Le() != null => new LeExpr(left, right),
            var op when op.Gt() != null => new GtExpr(left, right),
            var op when op.Ge() != null => new GeExpr(left, right),
            var op when op.RegexMatch() != null => new MatchExpr(left, right),
            var op when op.NotRegexMatch() != null => new NotMatchExpr(left, right),
            var op when op.NullCoalesce() != null => new NullCoalescingExpr(left, right),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitCompositionalExpr(RedisQLParser.CompositionalExprContext context)
    {
        if (context.compositionalExpr() == null) return base.VisitCompositionalExpr(context);
        var left = context.compositionalExpr().Accept(this);
        var right = context.additiveExpr().Accept(this);
        return context.compositionalOp() switch
        {
            var op when op.FromTo() != null => new RangeExpr(left, right),
            var op when op.With() != null => new WithExpr(left, right),
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
            var op when op.BitAnd() != null => new BitAndExpr(left, right),
            var op when op.BitOr() != null => new BitOrExpr(left, right),
            var op when op.BitXor() != null => new BitXorExpr(left, right),
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
            var op when op.BitLShift() != null => new BitLShiftExpr(left, right),
            var op when op.BitRShift() != null => new BitRShiftExpr(left, right),
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
            var op when op.BitNot() != null => new BitNotExpr(operand),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitPrimary(RedisQLParser.PrimaryContext context)
    {
        Value? value = context switch
        {
            var ctx when ctx.True() != null => BoolValue.True,
            var ctx when ctx.False() != null => BoolValue.False,
            var ctx when ctx.Null() != null => NullValue.Instance,
            _ => null,
        };

        if (value != null) return new LiteralExpr(value);

        if (context.PipelineValue() != null) return new IdentExpr(PipelineValueIdent);

        return context.Ident() != null
            ? new IdentExpr(context.Ident().GetText())
            : base.VisitPrimary(context);
    }

    public override Expr VisitPostFixedPrimary(RedisQLParser.PostFixedPrimaryContext context)
    {
        if (context.primary() != null) return context.primary().Accept(this);
        var first = context.postFixedPrimary().Accept(this);
        return context switch
        {
            var ctx when ctx.subscriptPostfix() != null => new SubscriptExpr(first, ctx.subscriptPostfix().Accept(this)),
            var ctx when ctx.fieldAccessPostfix() != null => new FieldAccessExpr(first, ctx.fieldAccessPostfix().Ident().GetText()),
            _ => throw new CompilationException("syntax does not allow this"),
        };
    }

    public override Expr VisitString(RedisQLParser.StringContext context)
    {
        Value value = context switch
        {
            var ctx when ctx.SingleQuotedString() != null => new StringValue(ParseString(ctx.SingleQuotedString().GetText())),
            var ctx when ctx.DoubleQuotedString() != null => new StringValue(ParseString(ctx.DoubleQuotedString().GetText())),
            _ => throw new CompilationException($"unexpected string literal: {context.GetText()}"),
        };
        return new LiteralExpr(value);
    }

    public override Expr VisitNumber(RedisQLParser.NumberContext context)
    {
        Value value = context switch
        {
            var ctx when ctx.Integer() != null && _parseIntAsReal => new RealValue(ParseReal(ctx.Integer().GetText())),
            var ctx when ctx.Integer() != null => IntegerValue.Of(ParseInteger(ctx.Integer().GetText())),
            var ctx when ctx.HexInteger() != null => IntegerValue.Of(ParseHexInteger(ctx.HexInteger().GetText())),
            var ctx when ctx.BinaryInteger() != null => IntegerValue.Of(ParseBinaryInteger(ctx.BinaryInteger().GetText())),
            var ctx when ctx.Real() != null => new RealValue(ParseReal(ctx.Real().GetText())),
            _ => throw new CompilationException($"unexpected number literal: {context.GetText()}"),
        };
        return new LiteralExpr(value);
    }

    public override Expr VisitFunctionInvocation(RedisQLParser.FunctionInvocationContext context) =>
        EmitFunctionInvocation(context, null);

    private Expr EmitFunctionInvocation(RedisQLParser.FunctionInvocationContext context, Expr? lastArgument)
    {
        var ident = context.Ident().GetText();
        var arguments = context.arguments()?.pipelineExpr().Select(a => a.Accept(this))
            ?? Enumerable.Empty<Expr>();
        if (lastArgument != null) arguments = arguments.Concat(new[] { lastArgument });
        return new FunctionInvocationExpr(ident, arguments.ToArray());
    }

    public override Expr VisitTuple(RedisQLParser.TupleContext context)
    {
        if (context.Ident() != null)
        {
            return new TupleExpr(
                new[] { context.pipelineExpr().Accept(this) },
                new Dictionary<string, int> { {context.Ident().GetText(), 0} });
        }

        var fieldIndicesByName = new Dictionary<string, int>();
        var fieldExpressions = new List<Expr>();
        var index = 0;
        foreach (var tupleItem in context.tupleItem())
        {
            fieldExpressions.Add(tupleItem.pipelineExpr().Accept(this));
            var fieldName = tupleItem.Ident()?.GetText();
            if (fieldName != null)
            {
                if (fieldIndicesByName.ContainsKey(fieldName)) throw new RuntimeException($"duplicate field in tuple: {fieldName}");
                fieldIndicesByName.Add(fieldName, index);
            }
            index++;
        }

        return new TupleExpr(fieldExpressions, fieldIndicesByName);
    }

    public override Expr VisitList(RedisQLParser.ListContext context)
    {
        var items = context.arguments()?.pipelineExpr().Select(a => a.Accept(this));
        return items != null
            ? new ListExpr(items.ToArray())
            : ListExpr.Empty;
    }

    public override Expr VisitThrowExpr(RedisQLParser.ThrowExprContext context) =>
        new ThrowExpr(context.expr().Accept(this));

    public override Expr VisitFuncBinding(RedisQLParser.FuncBindingContext context) =>
        new FuncBinding(
            context.Ident().GetText(),
            context.identList()?.Ident()
                .Select(ident => ident.GetText())
                .ToArray() ?? Array.Empty<string>(),
            context.letExpr().Accept(this));

    protected override Expr AggregateResult(Expr aggregate, Expr nextResult) =>
        (aggregate, nextResult) switch
        {
            (not null, null) => aggregate,
            (null, not null) => nextResult,
            (_, _) => base.AggregateResult(aggregate!, nextResult!),
        };

    private static string ParseString(string s) => s.StartsWith("\"") ? s.Trim('\"') : s.Trim('\'');
    private static long ParseInteger(string s) => long.Parse(s.Replace("_", string.Empty));
    private static long ParseHexInteger(string s) => Convert.ToInt64(s.Replace("_", string.Empty)[2..], 16);
    private static long ParseBinaryInteger(string s) => Convert.ToInt64(s.Replace("_", string.Empty)[2..], 2);
    private static double ParseReal(string s) => double.Parse(s.Replace("_", string.Empty), CultureInfo.InvariantCulture);
}
