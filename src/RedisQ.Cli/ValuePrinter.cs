using ConsoleTables;
using RedisQ.Core;
using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal class ValuePrinter
{
    private readonly Options _options;
    private readonly Func<string, Task<bool>>? _continuePrompt;

    public ValuePrinter(Options options, Func<string, Task<bool>>? continuePrompt)
    {
        _options = options;
        _continuePrompt = continuePrompt;
    }

    public async Task Print(Value value, TextWriter writer, string indent = "")
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
            case TupleValue tuple:
                PrintTuples(new[] { tuple }, writer, indent);
                break;
            default:
                await writer.WriteLineAsync($"{indent}{value.AsString()}");
                break;
        }
    }

    private static string JoinList(ListValue list, int? max) =>
        list.Count > max
            ? $"[{string.Join(", ", list.Select(v => v.AsString()).Take(max.Value))}, ...]"
            : $"[{string.Join(", ", list.Select(v => v.AsString()))}]";

    private async Task PrintEnumerable(IAsyncEnumerable<Value> values, TextWriter writer, string indent)
    {
        var chunks = values.Chunk(_options.ChunkSize);
        var count = 0;
        var cts = new CancellationTokenSource(_options.EvaluationTimeout);
        await foreach (var chunk in chunks.WithCancellation(cts.Token))
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

            count += chunk.Length;
            if (_continuePrompt != null && chunk.Length == _options.ChunkSize)
            {
                if (await _continuePrompt($"{indent}{count} element(s)") == false) break;
            }
            else
            {
                await writer.WriteLineAsync($"{indent}{count} element(s)");
            }
        }

        if (count == 0) await writer.WriteLineAsync($"{indent}0 elements");
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
