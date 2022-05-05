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
        try
        {
            switch (value)
            {
                case ListValue list when HasComplexValues(list):
                    await writer.WriteLineAsync();
                    await PrintEnumerable(list, writer, indent);
                    break;
                case ListValue list:
                    await writer.WriteLineAsync($"{indent}{JoinList(list, null)}");
                    break;
                case EnumerableValue enumerable:
                    await writer.WriteLineAsync();
                    await PrintEnumerable(enumerable, writer, indent);
                    break;
                default:
                    await writer.WriteLineAsync($"{indent}{value.AsString()}");
                    break;
            }
        }
        catch (RuntimeException e)
        {
            Console.WriteLine(_options.Verbose ? e : e.Message);
        }
    }

    private static string JoinList(ListValue list, int? max) =>
        list.Count > max
            ? $"[{string.Join(", ", list.Select(v => v.AsString()).Take(max.Value))}, ...]"
            : $"[{string.Join(", ", list.Select(v => v.AsString()))}]";

    private async Task PrintEnumerable(IAsyncEnumerable<Value> values, TextWriter writer, string indent)
    {
        const int chunkSize = 100;
        var chunks = values.Chunk(chunkSize);
        var count = 0;
        await foreach (var chunk in chunks)
        {
            try
            {
                if (chunk.First() is TupleValue)
                {
                    PrintTuples(chunk, writer, indent);
                }
                else
                {
                    foreach (var v in chunk)
                    {
                        await Print(v, writer, indent + "  ");
                    }
                    await writer.WriteLineAsync();
                }
            }
            catch (RuntimeException e)
            {
                Console.WriteLine(_options.Verbose ? e : e.Message);
            }
            count += chunk.Length;
            if (chunk.Length == chunkSize)
            {
                if (await PromptContinue(writer, $"{indent}Enumerated {count} element(s)")) break;
            }
            else
            {
                await writer.WriteLineAsync($"{indent}Enumerated {count} element(s)");
            }
        }
    }

    private static async Task<bool> PromptContinue(TextWriter writer, string prompt)
    {
        await writer.WriteAsync($"{prompt}. Q to abort, any other key to continue: ");
        var key = Console.ReadKey(intercept: false);
        await writer.WriteLineAsync();
        return key.Key == ConsoleKey.Q;
    }

    private static void PrintTuples(IReadOnlyCollection<Value> values, TextWriter writer, string indent)
    {
        var firstValue = (TupleValue) values.First();
        var columns = firstValue.FieldNames.Count > 0
            ? firstValue.FieldNames.ToArray()
            : Enumerable.Range(0, firstValue.Items.Count)
                .Select(n => n.ToString())
                .ToArray();

        var table = new ConsoleTable(columns).Configure(o => o.OutputTo = writer);
        foreach (var value in values)
        {
            if (value is TupleValue tuple) table.AddRow(tuple.Items.Select(item => ToCellString(item) as object).ToArray());
            else table.AddRow(value.AsString());
        }
        table.Write(Format.Minimal);
    }

    private static string ToCellString(Value value) =>
        value switch
        {
            ListValue list => JoinList(list, 100),
            EnumerableValue _ => "(enumerable)",
            _ => value.AsString(),
        };

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
