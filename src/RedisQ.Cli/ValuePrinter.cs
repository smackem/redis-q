using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal class ValuePrinter
{
    public async Task Print(Value value, TextWriter writer)
    {
        switch (value)
        {
            case ListValue list:
                await writer.WriteLineAsync($"{list.Count} elements:");
                await writer.WriteLineAsync(string.Join(", ", list.Select(v => v.AsString())));
                break;
            case EnumerableValue enumerable:
                var count = 0;
                await foreach (var v in enumerable)
                {
                    await writer.WriteLineAsync(v.AsString());
                    count++;
                }
                await writer.WriteLineAsync($"Found {count} elements");
                break;
            default:
                await writer.WriteLineAsync(value.AsString());
                break;
        }
    }
}
