using RedisQ.Core.Runtime;
using TextCopy;

namespace RedisQ.Cli;

public static class CliFunctions
{
    public static void Register(FunctionRegistry registry)
    {
        registry.Register(new FunctionDefinition("clip", 1, FuncClip));
        registry.Register(new FunctionDefinition("save", 2, FuncSave));
    }

    private static async Task<Value> FuncClip(Context ctx, Value[] arguments)
    {
        var options = new Options();
        var printer = new ValuePrinter(options, null);
        await using var writer = new StringWriter();
        await printer.Print(arguments[0], writer);
        var str = writer.ToString();
        await ClipboardService.SetTextAsync(str);
        return new StringValue($"{str.Length} characters copied to clipboard :-)");
    }

    private static async Task<Value> FuncSave(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue path == false) throw new RuntimeException($"save({arguments[0]}): incompatible operand, string expected");
        await using var stream = File.Create(path.Value);
        var options = new Options();
        if (arguments[1] is RedisValue value)
        {
            await stream.WriteAsync(value.Value);
            await stream.FlushAsync();
        }
        else
        {
            var writer = new StreamWriter(stream);
            var printer = new ValuePrinter(options, null);
            await printer.Print(arguments[1], writer);
            await writer.FlushAsync();
        }
        return new StringValue($"{stream.Position} bytes written to {path.Value} ;-)");
    }
}
