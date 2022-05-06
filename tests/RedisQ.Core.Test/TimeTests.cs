using System;
using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class TimeTests : TestBase
{
    [Fact]
    public async Task ConstructTimestamp()
    {
        var value = await Interpret(@"timestamp(""2022-05-01 23:40:50"", ""yyyy-MM-dd HH:mm:ss"")");
        var expected = new DateTime(2022, 5, 1, 23, 40, 50);
        Assert.Equal(new TimestampValue(new DateTimeOffset(expected)), value);
    }

    [Fact]
    public async Task ConstructFullTimestamp()
    {
        var value = await Interpret(@"timestamp(""2022-05-01 23:40:50.123 +02"", ""yyyy-MM-dd HH:mm:ss.fff zz"")");
        var expected = new DateTimeOffset(2022, 5, 1, 23, 40, 50, 123, TimeSpan.FromHours(2));
        Assert.Equal(new TimestampValue(expected), value);
    }

    [Fact]
    public async Task DeconstructFullTimestamp()
    {
        var value = await Interpret(@"
from ts in [timestamp(""2022-05-01 23:40:50.123"", ""yyyy-MM-dd HH:mm:ss.fff"")]
let x = deconstruct(ts)
select (x.year, x.month, x.day, x.hour, x.minute, x.second, x.millisecond)
|> first()
");
        Assert.Equal(TupleValue.Of(
            IntegerValue.Of(2022),
            IntegerValue.Of(5),
            IntegerValue.Of(1),
            IntegerValue.Of(23),
            IntegerValue.Of(40),
            IntegerValue.Of(50),
            IntegerValue.Of(123)), value);
    }

    [Fact]
    public async Task ConstructDuration()
    {
        var value1 = await Interpret(@"duration(""23:50:40"", ""g"")");
        Assert.Equal(new DurationValue(new TimeSpan(23, 50, 40)), value1);
        var value2 = await Interpret(@"duration(100, ""s"")");
        Assert.Equal(new DurationValue(TimeSpan.FromSeconds(100)), value2);
        var value3 = await Interpret(@"duration(100, ""ms"")");
        Assert.Equal(new DurationValue(TimeSpan.FromMilliseconds(100)), value3);
        var value4 = await Interpret(@"duration(100, ""m"")");
        Assert.Equal(new DurationValue(TimeSpan.FromMinutes(100)), value4);
        var value5 = await Interpret(@"duration(100, ""h"")");
        Assert.Equal(new DurationValue(TimeSpan.FromHours(100)), value5);
    }

    [Fact]
    public async Task ConstructDurationThrowsOnInvalidInput()
    {
        await Assert.ThrowsAsync<RuntimeException>(() => Interpret(@"duration(100, ""g"")"));
    }

    [Fact]
    public async Task ConvertDuration()
    {
        var value1 = await Interpret(@"convert(""s"", duration(100, ""s""))");
        Assert.Equal(new RealValue(100), value1);
        var value2 = await Interpret(@"convert(""ms"", duration(100, ""ms""))");
        Assert.Equal(new RealValue(100), value2);
        var value3 = await Interpret(@"convert(""m"", duration(100, ""m""))");
        Assert.Equal(new RealValue(100), value3);
        var value4 = await Interpret(@"convert(""h"", duration(100, ""h""))");
        Assert.Equal(new RealValue(100), value4);
    }

    [Fact]
    public async Task TimestampArithmetics()
    {
        var value = await Interpret(@"
from ts in [timestamp(""2022-05-01 23:40:35.123"", ""yyyy-MM-dd HH:mm:ss.fff"")]
select (
    ts + duration(10, ""s""),
    ts - duration(10, ""s""))
|> first()
");
        Assert.Equal(TupleValue.Of(
                new TimestampValue(new DateTimeOffset(new DateTime(2022, 05, 01, 23, 40, 45, 123))),
                new TimestampValue(new DateTimeOffset(new DateTime(2022, 05, 01, 23, 40, 25, 123)))),
            value);
    }

    [Fact]
    public async Task TimestampComparison()
    {
        var value = await Interpret(@"
from ts in [timestamp(""2022-05-01 23:40:35.123"", ""yyyy-MM-dd HH:mm:ss.fff"")]
select (
    ts == timestamp(""2022-05-01 23:40:35.123"", ""yyyy-MM-dd HH:mm:ss.fff""),
    ts > timestamp(""2022-05-01 23:40:20.123"", ""yyyy-MM-dd HH:mm:ss.fff""),
    ts < timestamp(""2022-05-01 23:40:45.123"", ""yyyy-MM-dd HH:mm:ss.fff""))
|> first()
");
        Assert.Equal(TupleValue.Of(BoolValue.True, BoolValue.True, BoolValue.True), value);
    }

    [Fact]
    public async Task DurationArithmetics()
    {
        var value = await Interpret(@"
from ts in [duration(1, ""m"")]
select (
    ts + duration(10, ""s""),
    ts - duration(10, ""s""))
|> first()
");
        Assert.Equal(TupleValue.Of(
                new DurationValue(new TimeSpan(0, 1, 10)),
                new DurationValue(new TimeSpan(0, 0, 50))),
            value);
    }

    [Fact]
    public async Task DurationComparison()
    {
        var value = await Interpret(@"
from ts in [duration(1, ""m"")]
select (
    ts == duration(60, ""s""),
    ts > duration(50, ""s""),
    ts < duration(70, ""s""))
|> first()
");
        Assert.Equal(TupleValue.Of(BoolValue.True, BoolValue.True, BoolValue.True), value);
    }
}
