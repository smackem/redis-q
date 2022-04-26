using ConsoleTables;
using RedisQ.Core;
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
                await PrintEnumerable(enumerable, writer, indent);
                // var count = 0;
                // try
                // {
                //     await foreach (var v in enumerable)
                //     {
                //         await Print(v, writer, indent + "  ");
                //         count++;
                //     }
                // }
                // catch (RuntimeException e)
                // {
                //     Console.WriteLine(_options.Verbose ? e : e.Message);
                // }
                // await writer.WriteLineAsync($"{indent}Found {count} element(s)");
                break;
            default:
                await writer.WriteLineAsync($"{indent}{value.AsString()}");
                break;
        }
    }

    private async Task PrintEnumerable(IAsyncEnumerable<Value> values, TextWriter writer, string indent)
    {
        var chunks = values.Chunk(100);
        await foreach (var chunk in chunks)
        {
            if (chunk.First() is TupleValue) PrintTuples(chunk, writer, indent);
        }
    }

    private void PrintTuples(IReadOnlyCollection<Value> values, TextWriter writer, string indent)
    {
        var firstValue = (TupleValue) values.First();
        var columns = Enumerable.Range(1, firstValue.Items.Count)
            .Select(n => n.ToString())
            .ToArray();
        var table = new ConsoleTable(columns).Configure(o => o.OutputTo = writer);
        foreach (var value in values)
        {
            if (value is TupleValue tuple) table.AddRow(tuple.Items.Select(item => item.AsString() as object).ToArray());
            else table.AddRow(value.AsString());
        }
        table.Write(Format.Minimal);
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
