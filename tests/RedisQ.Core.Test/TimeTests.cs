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
        var value = await Interpret(@"timestamp(""yyyy-MM-dd HH:mm:ss"", ""2022-05-01 23:40:50"")");
        var expected = new DateTime(2022, 5, 1, 23, 40, 50);
        Assert.Equal(new TimestampValue(new DateTimeOffset(expected)), value);
    }

    [Fact]
    public async Task ConstructFullTimestamp()
    {
        var value = await Interpret(@"timestamp(""yyyy-MM-dd HH:mm:ss.fff zz"", ""2022-05-01 23:40:50.123 +02"")");
        var expected = new DateTimeOffset(2022, 5, 1, 23, 40, 50, 123, TimeSpan.FromHours(2));
        Assert.Equal(new TimestampValue(expected), value);
    }

    [Fact]
    public async Task DeconstructFullTimestamp()
    {
        var value = await Interpret(@"
from ts in [timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:50.123"")]
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
        var value1 = await Interpret(@"duration(""g"", ""23:50:40"")");
        Assert.Equal(new DurationValue(new TimeSpan(23, 50, 40)), value1);
        var value2 = await Interpret(@"duration(""s"", 100)");
        Assert.Equal(new DurationValue(TimeSpan.FromSeconds(100)), value2);
        var value3 = await Interpret(@"duration(""ms"", 100)");
        Assert.Equal(new DurationValue(TimeSpan.FromMilliseconds(100)), value3);
        var value4 = await Interpret(@"duration(""m"", 100)");
        Assert.Equal(new DurationValue(TimeSpan.FromMinutes(100)), value4);
        var value5 = await Interpret(@"duration(""h"", 100)");
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
        var value1 = await Interpret(@"convert(""s"", duration(""s"", 100))");
        Assert.Equal(new RealValue(100), value1);
        var value2 = await Interpret(@"convert(""ms"", duration(""ms"", 100))");
        Assert.Equal(new RealValue(100), value2);
        var value3 = await Interpret(@"convert(""m"", duration(""m"", 100))");
        Assert.Equal(new RealValue(100), value3);
        var value4 = await Interpret(@"convert(""h"", duration(""h"", 100))");
        Assert.Equal(new RealValue(100), value4);
    }

    [Fact]
    public async Task TimestampArithmetics()
    {
        var value = await Interpret(@"
from ts in [timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:35.123"")]
select (
    ts + duration(""s"", 10),
    ts - duration(""s"", 10))
|> first()
");
        Assert.Equal(TupleValue.Of(
                new DurationValue(new TimeSpan(0, 23, 40, 35, 123)),
                new TimestampValue(new DateTimeOffset(new DateTime(2022, 05, 01, 23, 40, 45, 123))),
                new TimestampValue(new DateTimeOffset(new DateTime(2022, 05, 01, 23, 40, 25, 123)))),
            value);
    }

    [Fact]
    public async Task TimestampComparison()
    {
        var value = await Interpret(@"
let ts = timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:35.123"") in
let eq = ts == timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:35.123"") in
let gt = ts > timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:20.123"") in
let lt = ts < timestamp(""yyyy-MM-dd HH:mm:ss.fff"", ""2022-05-01 23:40:45.123"") in
    (eq, gt, lt) 
");
        Assert.Equal(TupleValue.Of(BoolValue.True, BoolValue.True, BoolValue.True), value);
    }

    [Fact]
    public async Task DurationArithmetics()
    {
        var value = await Interpret(@"
from ts in [duration(""m"", 1)]
select (
    ts + duration(""s"", 10),
    ts - duration(""s"", 10))
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
from ts in [duration(""m"", 1)]
select (
    ts == duration(""s"", 60),
    ts > duration(""s"", 50),
    ts < duration(""s"", 70))
|> first()
");
        Assert.Equal(TupleValue.Of(BoolValue.True, BoolValue.True, BoolValue.True), value);
    }
}
