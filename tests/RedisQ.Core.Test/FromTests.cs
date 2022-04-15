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
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(1), v),
            v => Assert.Equal(new IntegerValue(2), v),
            v => Assert.Equal(new IntegerValue(3), v));
    }

    [Fact]
    public async Task SelectSimpleMapping()
    {
        const string source = @"
from x in [1, 2, 3] select x + 1
";
        var value = await Interpret(source);
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(2), v),
            v => Assert.Equal(new IntegerValue(3), v),
            v => Assert.Equal(new IntegerValue(4), v));
    }

    [Fact]
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
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(2), v));
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
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(6), v),
            v => Assert.Equal(new IntegerValue(12), v),
            v => Assert.Equal(new IntegerValue(24), v));
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
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(1), v),
            v => Assert.Equal(new IntegerValue(2), v),
            v => Assert.Equal(new IntegerValue(3), v));
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
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v => Assert.Equal(new IntegerValue(2), v));
    }

    [Fact]
    public async Task SelectNestedFrom()
    {
        await using var writer = new StringWriter();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));

        const string source = @"
from x in [1, 2, 3]
from y in [1]
select x + y
";
        var value = await Interpret(source);
        _out.WriteLine(writer.GetStringBuilder().ToString());
        _out.WriteLine("depp");
        Assert.IsType<EnumerableValue>(value);
        var coll = (EnumerableValue) value;
        var values = await Helpers.Collect(coll);
        Assert.Collection(values,
            v =>
            {
                Assert.IsType<EnumerableValue>(v);
                var innerColl = (EnumerableValue) v;
                var innerValues = Helpers.Collect(innerColl).Result;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(new IntegerValue(2), iv));
            },
            v =>
            {
                Assert.IsType<EnumerableValue>(v);
                var innerColl = (EnumerableValue) v;
                var innerValues = Helpers.Collect(innerColl).Result;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(new IntegerValue(3), iv));
            },
            v =>
            {
                Assert.IsType<EnumerableValue>(v);
                var innerColl = (EnumerableValue) v;
                var innerValues = Helpers.Collect(innerColl).Result;
                Assert.Collection(innerValues,
                    iv => Assert.Equal(new IntegerValue(4), iv));
            });
    }
}
