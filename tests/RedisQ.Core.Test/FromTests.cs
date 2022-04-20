using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace RedisQ.Core.Test;

public class FromTests : TestBase
{
    private readonly ITestOutputHelper _out;

    public FromTests(ITestOutputHelper @out) => _out = @out;

    [Fact]
    public async Task SelectIdentity()
    {
        const string source = @"
from x in [1, 2, 3] select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }

    [Fact]
    public async Task SelectSimpleMapping()
    {
        const string source = @"
from x in [1, 2, 3] select x + 1
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v),
            v => Assert.Equal(IntegerValue.Of(4), v));
    }

    //[Fact]
    public async Task ThrowsOnNonEnumerableSource()
    {
        const string source = @"
from x in 1 select x
";
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(source));
    }

    [Fact]
    public async Task SelectFilter()
    {
        const string source = @"
from x in [1, 2, 3]
where x % 2 == 0
select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v));
    }

    [Fact]
    public async Task SelectMultiFilter()
    {
        const string source = @"
from x in [1, 2, 3, 4, 5, 6, 7, 12, 24, 100]
where x % 2 == 0
where x % 3 == 0
select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(6), v),
            v => Assert.Equal(IntegerValue.Of(12), v),
            v => Assert.Equal(IntegerValue.Of(24), v));
    }

    [Fact]
    public async Task SelectBindings()
    {
        const string source = @"
from x in [1, 2, 3]
let y = x
select y
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }

    [Fact]
    public async Task SelectFiltersAndBindings()
    {
        const string source = @"
from x in [1, 2, 3]
let y = x % 2
where y == 0
select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v));
    }

    [Fact]
    public async Task SelectCrossJoinedFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
from y in [1, 2]
select x + y
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v),
            v => Assert.Equal(IntegerValue.Of(3), v),
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(5), v));
    }

    [Fact]
    public async Task SelectFilteredCrossJoinedFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
where x % 2 == 0
from y in [1, 2, 3, 4, 5]
where y % 2 == 0
select x + y
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(6), v));
    }

    [Fact]
    public async Task SelectFilteredCrossJoinedFromWithBindings()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
let temp = x % 2
where temp == 0
from y in [1, 2, 3, 4, 5]
let temp = y % 2
where temp == 0
select x + y
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(6), v));
    }

    [Fact]
    public async Task SelectNestedFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
let multiples =
    from y in [1, 2] select x * y
from m in multiples
select m
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(3), v),
            v => Assert.Equal(IntegerValue.Of(6), v));
    }

    [Fact]
    public async Task SelectDeeplyNestedFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
let multiples =
    from y in [1, 2]
    let h = from z in [2] select y * z
    from i in h select x * i
from m in multiples
select m
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(4), v),
            v => Assert.Equal(IntegerValue.Of(8), v),
            v => Assert.Equal(IntegerValue.Of(6), v),
            v => Assert.Equal(IntegerValue.Of(12), v));
    }

    [Fact]
    public async Task SelectInnerFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
select
    from y in [1, 2] select x * y
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue) value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v =>
            {
                Assert.IsType<ListValue>(v);
                var innerValues = (ListValue) v;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(IntegerValue.Of(1), iv),
                    iv => Assert.Equal(IntegerValue.Of(2), iv));
            },
            v =>
            {
                Assert.IsType<ListValue>(v);
                var innerValues = (ListValue) v;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(IntegerValue.Of(2), iv),
                    iv => Assert.Equal(IntegerValue.Of(4), iv));
            },
            v =>
            {
                Assert.IsType<ListValue>(v);
                var innerValues = (ListValue) v;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(IntegerValue.Of(3), iv),
                    iv => Assert.Equal(IntegerValue.Of(6), iv));
            });
    }
    
    [Fact]
    public async Task SelectLimited()
    {
        const string source = @"
from x in [1, 2, 3] limit 2 select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v));
    }

    [Fact]
    public async Task SelectZeroLimited()
    {
        const string source = @"
from x in [1, 2, 3] limit 0 select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Empty(values);
    }

    [Fact]
    public async Task SelectLimitedExceedingCount()
    {
        const string source = @"
from x in [1, 2, 3] limit 100 select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(1), v),
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }

    [Fact]
    public async Task SelectLimitedWithOffset()
    {
        const string source = @"
from x in [1, 2, 3] limit 2 offset 1 select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Collection(values,
            v => Assert.Equal(IntegerValue.Of(2), v),
            v => Assert.Equal(IntegerValue.Of(3), v));
    }

    [Fact]
    public async Task SelectLimitedWithOffsetOutOfRange()
    {
        const string source = @"
from x in [1, 2, 3] limit 2 offset 3 select x
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue)value;
        var values = await coll.Collect();
        Assert.Empty(values);
    }
}
