using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal class ValuePrinter
{
    private readonly Options _options;

    public ValuePrinter(Options options) => _options = options;
    
    public async Task Print(Value value, TextWriter writer, string indent = "")
    {
        switch (value)
        {
            case ListValue list when HasComplexValues(list):
                await writer.WriteLineAsync($"{indent}{list.Count} element(s):");
                foreach (var v in list)
                {
                    await Print(v, writer, indent + "  ");
                }
                break;
            case ListValue list:
                await writer.WriteLineAsync($"{indent}[{string.Join(", ", list.Select(v => v.AsString()))}]");
                break;
            case EnumerableValue enumerable:
                var count = 0;
                try
                {
                    await foreach (var v in enumerable)
                    {
                        await Print(v, writer, indent + "  ");
                        count++;
                    }
                }
                catch (RuntimeException e)
                {
                    Console.WriteLine(_options.Verbose ? e : e.Message);
                }
                await writer.WriteLineAsync($"{indent}Found {count} element(s)");
                break;
            default:
                await writer.WriteLineAsync($"{indent}{value.AsString()}");
                break;
        }
    }

    private static bool HasComplexValues(ListValue list) =>
        list.Any(IsComplexValue);

    private static bool IsComplexValue(Value value) =>
        value switch
        {
            EnumerableValue _ => true,
            TupleValue _ => true,
            RedisValue v when v.Value.Length() > 60 => true,
            _ => false,
        };
}
