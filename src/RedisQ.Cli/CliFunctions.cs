using System.Diagnostics.CodeAnalysis;
using RedisQ.Core.Runtime;
using TextCopy;

namespace RedisQ.Cli;

public static class CliFunctions
{
    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public static void Register(FunctionRegistry registry)
    {
        registry.Register(new("clip", 1, FuncClip, "(any) -> dummy: string"));
        registry.Register(new("save", 2, FuncSave, "(path: string, value: any) -> dummy: string"));
        registry.Register(new("trace", 1, FuncTrace, "(value: any) -> value"));
    }

    private static Task<Value> FuncTrace(Context ctx, Value[] arguments)
    {
        var value = arguments[0];
        var timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.WriteLine($"{timeStr} {value.AsString()}");
        return Task.FromResult(value);
    }

    private static async Task<Value> FuncClip(Context ctx, Value[] arguments)
    {
        var options = new Options(); // override options
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
        if (arguments[1] is RedisValue value)
        {
            await stream.WriteAsync(value.Value);
            await stream.FlushAsync();
        }
        else
        {
            var writer = new StreamWriter(stream);
            var options = new Options(); // override options
            var printer = new ValuePrinter(options, null);
            await printer.Print(arguments[1], writer);
            await writer.FlushAsync();
        }
        return new StringValue($"{stream.Position} bytes written to {path.Value} ;-)");
    }
}
