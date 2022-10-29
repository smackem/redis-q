using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RedisQ.Core.Runtime;

public partial class FunctionRegistry
{
    private static readonly Random Rng = new();

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    private void RegisterCommonFunctions()
    {
        Register(new("size", 1, FuncSize, "(list or string) -> int"));
        Register(new("count", 1, FuncCount, "(enumerable) -> int"));
        Register(new("int", 1, FuncInt, "(any) -> int or null"));
        Register(new("real", 1, FuncReal, "(any) -> real or null"));
        Register(new("bool", 1, FuncBool, "(any) -> bool or null"));
        Register(new("string", 1, FuncString, "(any) -> string"));
        Register(new("lower", 1, FuncLower, "(string) -> string"));
        Register(new("upper", 1, FuncUpper, "(string) -> string"));
        Register(new("collect", 1, FuncCollect, "(enumerable) -> list"));
        Register(new("join", 2, FuncJoin, "(separator: string, enumerable) -> string"));
        Register(new("distinct", 1, FuncDistinct, "(enumerable) -> enumerable"));
        Register(new("sum", 1, FuncSum, "(enumerable) -> number or null"));
        Register(new("avg", 1, FuncAvg, "(enumerable) -> number or null"));
        Register(new("min", 1, FuncMin, "(enumerable) -> number or null"));
        Register(new("max", 1, FuncMax, "(enumerable) -> number or null"));
        Register(new("reverse", 1, FuncReverse, "(enumerable) -> enumerable"));
        Register(new("sort", 1, FuncSort, "(enumerable) -> enumerable"));
        Register(new("match", 2, FuncMatch, "(pattern: string, input: string) -> list of matched groups"));
        Register(new("first", 1, FuncFirst, "(enumerable) -> any"));
        Register(new("any", 1, FuncAny, "(enumerable) -> bool"));
        Register(new("enumerate", 1, FuncEnumerate, "(list) -> enumerable"));
        Register(new("timestamp", 2, FuncTimestamp, "(format: string, input: string) -> timestamp"));
        Register(new("now", 0, FuncNow, "() -> timestamp"));
        Register(new("deconstruct", 1, FuncDeconstruct, "(timestamp) -> tuple of (year, month, day, hour, minute, second, millisecond)"));
        Register(new("duration", 2, FuncDuration, "(formatOrUnit: string, input: string or number) -> duration"));
        Register(new("convert", 2, FuncConvert, "(unit: 'h' or 'm' or 's' or 'ms', duration) -> real"));
        Register(new("random", 2, FuncRandom, "(minInclusive: int, maxExclusive: int) -> int"));
        Register(new("pow", 2, FuncPow, "(base: real, exponent: real) -> real"));
        Register(new("exp", 1, FuncExp, "(x: real) -> real"));
        Register(new("abs", 1, FuncAbs, "(number) -> number"));
        Register(new("sign", 1, FuncSign, "(number) -> int"));
        Register(new("hypot", 2, FuncHypot, "(real, real) -> real"));
        Register(new("sqrt", 1, FuncSqrt, "(real) -> real"));
        Register(new("log", 1, FuncLog, "(real) -> real"));
        Register(new("log2", 1, FuncLog2, "(real) -> real"));
        Register(new("log10", 1, FuncLog10, "(real) -> real"));
        Register(new("sin", 1, FuncSin, "(real) -> real"));
        Register(new("cos", 1, FuncCos, "(real) -> real"));
        Register(new("tan", 1, FuncTan, "(real) -> real"));
        Register(new("asin", 1, FuncAsin, "(real) -> real"));
        Register(new("acos", 1, FuncAcos, "(real) -> real"));
        Register(new("atan", 1, FuncAtan, "(real) -> real"));
        Register(new("uuid", 0, FuncUuid, "() -> string"));
        Register(new("trim", 1, FuncTrim, "(string) -> string"));
        Register(new("replace", 3, FuncReplace, "(pattern: string, replacement: string, input: string) -> string"));
        Register(new("indexof", 2, FuncIndexOf, "(value: any, searchee: list|string) -> int"));
        Register(new("split", 2, FuncSplit, "(pattern: string, input: string) -> list of strings"));
        Register(new("char", 1, FuncChar, "(code: int) -> string"));
        Register(new("nl", 0, (_, _) => Task.FromResult<Value>(new StringValue(Environment.NewLine)), "() -> string"));
        Register(new("pi", 0, (_, _) => Task.FromResult<Value>(new RealValue(Math.PI)), "() -> real"));
        Register(new("e", 0, (_, _) => Task.FromResult<Value>(new RealValue(Math.E)), "() -> real"));
        Register(new("parse", 1, FuncParse, "(json: string) -> value"));
        Register(new("json", 1, FuncJson, "(value) -> string"));
        Register(new("typeof", 1, FuncTypeOf, "(value) -> string"));
        Register(new("hex", 1, FuncHex, "(integer) -> string"));
        Register(new("bin", 1, FuncBin, "(integer) -> string"));
        Register(new("today", 0, FuncToday, "() -> timestamp"));
        Register(new("date", 1, FuncDate, "(timestamp) -> timestamp"));
    }

    private static Task<Value> FuncToday(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new TimestampValue(DateTimeOffset.Now.Date));

    private static Task<Value> FuncDate(Context ctx, Value[] arguments)
    {
        if (arguments[0] is TimestampValue ts == false) throw new RuntimeException($"date({arguments[0]}): incompatible operand, timestamp expected");
        return Task.FromResult<Value>(new TimestampValue(ts.Value.Date));
    }

    private static Task<Value> FuncHex(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IntegerValue n == false) throw new RuntimeException($"hex({arguments[0]}): incompatible operand, integer expected");
        var s = Convert.ToString(n.Value, 16);
        return Task.FromResult<Value>(new StringValue(s));
    }

    private static Task<Value> FuncBin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IntegerValue n == false) throw new RuntimeException($"bin({arguments[0]}): incompatible operand, integer expected");
        var s = Convert.ToString(n.Value, 2);
        return Task.FromResult<Value>(new StringValue(s));
    }

    private static Task<Value> FuncTypeOf(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(arguments[0].TypeName));

    private static Task<Value> FuncJson(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(JsonPath.ToJson(arguments[0])));

    private static Task<Value> FuncParse(Context ctx, Value[] arguments)
    {
        var json = arguments[0].AsString();
        return Task.FromResult(JsonPath.Parse(json));
    }

    private static Task<Value> FuncChar(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IntegerValue code == false) throw new RuntimeException($"char({arguments[0]}): incompatible operand, integer expected");
        var ch = (char) code.Value;
        return Task.FromResult<Value>(new StringValue(ch.ToString()));
    }

    private static Task<Value> FuncUuid(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(Guid.NewGuid().ToString()));

    private static Task<Value> FuncTrim(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue str == false) throw new RuntimeException($"trim({arguments[0]}): incompatible operand, string expected");
        return Task.FromResult<Value>(new StringValue(str.Value.Trim()));
    }

    private static Task<Value> FuncReplace(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue pattern == false) throw new RuntimeException($"replace({arguments[0]}): incompatible operand, string expected");
        if (arguments[1] is StringValue replacement == false) throw new RuntimeException($"replace({arguments[1]}): incompatible operand, string expected");
        if (arguments[2] is StringValue str == false) throw new RuntimeException($"replace({arguments[2]}): incompatible operand, string expected");
        var result = Regex.Replace(str.Value, pattern.Value, replacement.Value);
        return Task.FromResult<Value>(new StringValue(result));
    }

    private static Task<Value> FuncIndexOf(Context ctx, Value[] arguments)
    {
        switch (arguments[1])
        {
            case StringValue str:
            {
                var subStr = arguments[0].AsString();
                Value index = str.Value.IndexOf(subStr, StringComparison.Ordinal) switch
                {
                    < 0 => NullValue.Instance,
                    var i => IntegerValue.Of(i),
                };
                return Task.FromResult(index);
            }
            case ListValue list:
            {
                Value index = list.IndexOf(arguments[0]) switch
                {
                    null => NullValue.Instance,
                    var i => IntegerValue.Of(i.Value),
                };
                return Task.FromResult(index);
            }
            default:
                throw new RuntimeException($"indexof({arguments[1]}): incompatible operand, string or list expected");
        }
    }

    private static Task<Value> FuncSplit(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue pattern == false) throw new RuntimeException($"replace({arguments[0]}): incompatible operand, string expected");
        if (arguments[1] is StringValue str == false) throw new RuntimeException($"replace({arguments[1]}): incompatible operand, string expected");
        var result = Regex.Split(str.Value, pattern.Value);
        var value = new ListValue(result.Select(s => new StringValue(s)).ToArray());
        return Task.FromResult<Value>(value);
    }

    private static Task<Value> FuncSize(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => Task.FromResult<Value>(IntegerValue.Of(list.Count)),
            StringValue str => Task.FromResult<Value>(IntegerValue.Of(str.Value.Length)),
            IRedisValue val => Task.FromResult<Value>(IntegerValue.Of(val.AsRedisValue().Length())),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static async Task<Value> FuncCount(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => IntegerValue.Of(list.Count),
            EnumerableValue enumerable => IntegerValue.Of(await CountEnumerable(enumerable).ConfigureAwait(false)),
            _ => NullValue.Instance,
        };

    private static async Task<int> CountEnumerable(EnumerableValue enumerable)
    {
        var count = 0;
        await foreach (var _ in enumerable.ConfigureAwait(false)) count++;
        return count;
    }
    
    private static Task<Value> FuncInt(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            IntegerValue n => Task.FromResult<Value>(n),
            RealValue real => Task.FromResult<Value>(IntegerValue.Of((long) real.Value)),
            StringValue str => Task.FromResult<Value>(
                int.TryParse(str.Value, out var n) ? IntegerValue.Of(n) : NullValue.Instance),
            IRedisValue val => Task.FromResult<Value>(
                val.AsRedisValue().TryParse(out int n) ? IntegerValue.Of(n) : NullValue.Instance),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static Task<Value> FuncReal(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            IntegerValue n => Task.FromResult<Value>(new RealValue(n.Value)),
            RealValue real => Task.FromResult<Value>(real),
            StringValue str => Task.FromResult<Value>(
                double.TryParse(str.Value, out var d) ? new RealValue(d) : NullValue.Instance),
            IRedisValue val => Task.FromResult<Value>(
                val.AsRedisValue().TryParse(out double d) ? new RealValue(d) : NullValue.Instance),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static Task<Value> FuncBool(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list =>Task.FromResult<Value>(BoolValue.Of(list.AsBoolean())), 
            RangeValue v => Task.FromResult<Value>(BoolValue.Of(v.AsBoolean())),
            EnumerableValue or TupleValue => Task.FromResult<Value>(NullValue.Instance),
            var v => Task.FromResult<Value>(BoolValue.Of(v.AsBoolean())),
        };

    private static Task<Value> FuncString(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            EnumerableValue => Task.FromResult<Value>(NullValue.Instance),
            var v => Task.FromResult<Value>(new StringValue(v.AsString())),
        };

    private static Task<Value> FuncLower(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(arguments[0].AsString().ToLower()));

    private static Task<Value> FuncUpper(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(arguments[0].AsString().ToUpper()));

    private static async Task<Value> FuncCollect(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            EnumerableValue e => new ListValue(await e.Collect(1000).ConfigureAwait(false)),
            _ => throw new RuntimeException($"collect({arguments[0]}): incompatible operand, enumerable expected"), 
        };

    private static async Task<Value> FuncJoin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue separator == false) throw new RuntimeException($"join({arguments[0]}): incompatible operand, string expected");
        if (arguments[1] is EnumerableValue coll == false) throw new RuntimeException($"join({arguments[1]}): incompatible operand, enumerable expected");

        var sb = new StringBuilder();
        var count = 0;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (count >= 1000) break;
            if (count != 0) sb.Append(separator.Value);
            sb.Append(value.AsString());
            count++;
        }

        return new StringValue(sb.ToString());
    }

    private static Task<Value> FuncDistinct(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"distinct({arguments[0]}): incompatible operand, enumerable expected");
        var set = new HashSet<Value>();

        async IAsyncEnumerable<Value> Walk()
        {
            await foreach (var value in coll.ConfigureAwait(false))
            {
                if (set.Add(value)) yield return value;
            }
        }

        return Task.FromResult<Value>(new EnumerableValue(Walk()));
    }

    private static async Task<Value> FuncSum(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"sum({arguments[0]}): incompatible operand, enumerable expected");
        Value? sum = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (value is NullValue) continue;

            sum = sum != null ? ValueOperations.Add(sum, value) : value;
        }

        return sum ?? NullValue.Instance;
    }

    private static async Task<Value> FuncAvg(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        var sum = 0.0;
        var count = 0;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (value is IRealValue real == false) continue;

            sum += real.AsRealValue();
            count++;
        }

        return count > 0 ? new RealValue(sum / count) : NullValue.Instance;
    }

    private static async Task<Value> FuncMin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        Value? min = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (min == null)
            {
                min = value;
                continue;
            }
            if (ValueComparer.Default.Compare(value, min) < 0) min = value;
        }

        return min ?? NullValue.Instance;
    }

    private static async Task<Value> FuncMax(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        Value? max = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (max == null)
            {
                max = value;
                continue;
            }
            if (ValueComparer.Default.Compare(value, max) > 0) max = value;
        }

        return max ?? NullValue.Instance;
    }

    private static async Task<Value> FuncReverse(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"reverse({arguments[0]}): incompatible operand, enumerable expected");
        var stack = new Stack<Value>();

        await foreach (var value in coll.ConfigureAwait(false))
        {
            stack.Push(value);
        }

        return coll is ListValue
            ? new ListValue(stack.ToArray())
            : new EnumerableValue(AsyncEnumerable.FromCollection(stack));
    }

    private static async Task<Value> FuncSort(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"sort({arguments[0]}): incompatible operand, enumerable expected");
        var list = await coll.Collect().ConfigureAwait(false);

        if (list is List<Value> l) l.Sort(ValueComparer.Default);
        else list = list.OrderBy(value => value, ValueComparer.Default).ToArray();

        return new ListValue(list);
    }

    private static Task<Value> FuncMatch(Context ctx, Value[] arguments)
    {
        var pattern = arguments[0].AsString();
        var input = arguments[1].AsString();
        var match = Regex.Match(input, pattern);
        var groupList = match.Success
            ? new ListValue(match.Groups.Values.Select(g => new StringValue(g.Value)).ToArray())
            : ListValue.Empty;
        return Task.FromResult<Value>(groupList);
    }

    private static async Task<Value> FuncFirst(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"first({arguments[0]}): incompatible operand, enumerable expected");
        await foreach (var v in coll.ConfigureAwait(false))
        {
            if (v is NullValue) continue;
            return v;
        }
        return NullValue.Instance;
    }

    private static async Task<Value> FuncAny(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"any({arguments[0]}): incompatible operand, enumerable expected");
        await foreach (var v in coll.ConfigureAwait(false))
        {
            if (v is NullValue) continue;
            return BoolValue.True;
        }
        return BoolValue.False;
    }

    private static Task<Value> FuncEnumerate(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"enumerate({arguments[0]}): incompatible operand, enumerable expected");
        return Task.FromResult<Value>(new EnumerableValue(coll));
    }

    private static Task<Value> FuncTimestamp(Context ctx, Value[] arguments)
    {
        var format = arguments[0] switch
        {
            StringValue fmt => fmt.Value,
            NullValue => TimestampValue.StandardFormatString,
            _ => throw new RuntimeException($"timestamp({arguments[0]}): incompatible operand, string expected"),
        };
        if (arguments[1] is StringValue s == false) throw new RuntimeException($"timestamp({arguments[1]}): incompatible operand, string expected");
        var time = DateTimeOffset.ParseExact(s.Value, format, CultureInfo.InvariantCulture);
        return Task.FromResult<Value>(new TimestampValue(time));
    }

    private static Task<Value> FuncDeconstruct(Context ctx, Value[] arguments)
    {
        if (arguments[0] is TimestampValue tsv == false) throw new RuntimeException($"deconstruct({arguments[0]}): incompatible operand, timestamp expected");
        var ts = tsv.Value;
        var tuple = TupleValue.Of(
            ("year", IntegerValue.Of(ts.Year)),
            ("month", IntegerValue.Of(ts.Month)),
            ("day", IntegerValue.Of(ts.Day)),
            ("hour", IntegerValue.Of(ts.Hour)),
            ("minute", IntegerValue.Of(ts.Minute)),
            ("second", IntegerValue.Of(ts.Second)),
            ("millisecond", IntegerValue.Of(ts.Millisecond)));
        return Task.FromResult<Value>(tuple);
    }

    private static Task<Value> FuncDuration(Context ctx, Value[] arguments)
    {
        var timeSpan = (arguments[1], arguments[0]) switch
        {
            (IntegerValue n, StringValue {Value: "ms" or "milliseconds"}) => TimeSpan.FromMilliseconds(n.Value),
            (IntegerValue n, StringValue {Value: "s" or "seconds"}) => TimeSpan.FromSeconds(n.Value),
            (RealValue r, StringValue {Value: "s" or "seconds"}) => TimeSpan.FromSeconds(r.Value),
            (IntegerValue n, StringValue {Value: "m" or "minutes"}) => TimeSpan.FromMinutes(n.Value),
            (RealValue r, StringValue {Value: "m" or "minutes"}) => TimeSpan.FromMinutes(r.Value),
            (IntegerValue n, StringValue {Value: "h" or "hours"}) => TimeSpan.FromHours(n.Value),
            (RealValue r, StringValue {Value: "h" or "hours"}) => TimeSpan.FromHours(r.Value),
            (StringValue s, StringValue format) => TimeSpan.ParseExact(s.Value, format.Value, CultureInfo.InvariantCulture),
            (StringValue s, NullValue) => TimeSpan.ParseExact(s.Value, DurationValue.StandardFormatString, CultureInfo.InvariantCulture),
            _ => throw new RuntimeException($"duration({arguments[0]}, {arguments[1]}): incompatible operands, (string, string or number) expected"),
        };
        return Task.FromResult<Value>(new DurationValue(timeSpan));
    }

    private static Task<Value> FuncConvert(Context ctx, Value[] arguments)
    {
        var value = (arguments[0], arguments[1]) switch
        {
            (StringValue {Value: "ms" or "milliseconds"}, DurationValue duration) => new RealValue(duration.Value.TotalMilliseconds),
            (StringValue {Value: "s" or "seconds"}, DurationValue duration) => new RealValue(duration.Value.TotalSeconds),
            (StringValue {Value: "m" or "minutes"}, DurationValue duration) => new RealValue(duration.Value.TotalMinutes),
            (StringValue {Value: "h" or "hours"}, DurationValue duration) => new RealValue(duration.Value.TotalHours),
            _ => throw new RuntimeException($"convert({arguments[0]}, {arguments[1]}): incompatible operands, (string, duration) expected"),
        };
        return Task.FromResult<Value>(value);
    }

    private static Task<Value> FuncRandom(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IntegerValue min == false) throw new RuntimeException($"random({arguments[0]}): incompatible operand, integer expected");
        if (arguments[1] is IntegerValue max == false) throw new RuntimeException($"random({arguments[1]}): incompatible operand, integer expected");
        var number = Rng.NextInt64(min.Value, max.Value);
        return Task.FromResult<Value>(IntegerValue.Of(number));
    }

    private static Task<Value> FuncPow(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue b == false) throw new RuntimeException($"pow({arguments[0]}): incompatible operand, number expected");
        if (arguments[1] is IRealValue e == false) throw new RuntimeException($"pow({arguments[1]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Pow(b.AsRealValue(), e.AsRealValue())));
    }

    private static Task<Value> FuncExp(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"exp({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Exp(a.AsRealValue())));
    }

    private static Task<Value> FuncAbs(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(arguments[0] switch
        {
            IntegerValue n => IntegerValue.Of(Math.Abs(n.Value)),
            RealValue r => new RealValue(Math.Abs(r.Value)),
            _ => throw new RuntimeException($"abs({arguments[0]}): incompatible operand, number expected"),
        });

    private static Task<Value> FuncSign(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"sign({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(IntegerValue.Of(Math.Sign(a.AsRealValue())));
    }

    private static Task<Value> FuncHypot(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"hypot({arguments[0]}): incompatible operand, number expected");
        if (arguments[1] is IRealValue b == false) throw new RuntimeException($"hypot({arguments[1]}): incompatible operand, number expected");
        var (ra, rb) = (a.AsRealValue(), b.AsRealValue());
        return Task.FromResult<Value>(new RealValue(Math.Sqrt(ra * ra + rb * rb)));
    }

    private static Task<Value> FuncSqrt(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"sqrt({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Sqrt(a.AsRealValue())));
    }

    private static Task<Value> FuncLog(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"log({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Log(a.AsRealValue())));
    }

    private static Task<Value> FuncLog2(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"log2({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Log2(a.AsRealValue())));
    }

    private static Task<Value> FuncLog10(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"log10({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Log10(a.AsRealValue())));
    }

    private static Task<Value> FuncSin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"sin({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Sin(a.AsRealValue())));
    }

    private static Task<Value> FuncCos(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"cos({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Cos(a.AsRealValue())));
    }

    private static Task<Value> FuncTan(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"tan({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Tan(a.AsRealValue())));
    }

    private static Task<Value> FuncAsin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"asin({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Asin(a.AsRealValue())));
    }

    private static Task<Value> FuncAcos(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"acos({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Acos(a.AsRealValue())));
    }

    private static Task<Value> FuncAtan(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRealValue a == false) throw new RuntimeException($"atan({arguments[0]}): incompatible operand, number expected");
        return Task.FromResult<Value>(new RealValue(Math.Atan(a.AsRealValue())));
    }

    private static Task<Value> FuncNow(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new TimestampValue(DateTimeOffset.Now));
}
