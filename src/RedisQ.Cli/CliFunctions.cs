using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using RedisQ.Core.Runtime;
using TextCopy;

namespace RedisQ.Cli;

public class CliFunctions
{
    private readonly Options _options;

    private CliFunctions(Options options)
    {
        _options = options;
    }
    
    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public static void Register(FunctionRegistry registry, Options options)
    {
        var instance = new CliFunctions(options);
        registry.Register(new("clip", 1, FuncClip, "(any) -> dummy: string"));
        registry.Register(new("save", 2, FuncSave, "(path: string, value: any) -> dummy: string"));
        registry.Register(new("trace", 1, FuncTrace, "(value: any) -> value"));
        registry.Register(new("cli", 1, instance.FuncCli, "(value: any) -> string"));
    }

    private async Task<Value> FuncCli(Context ctx, Value[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.CliFilePath,
            Arguments = arguments[0].AsString(),
        };
        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new FileNotFoundException();
            _options.CliFilePath = psi.FileName;
            var cts = new CancellationTokenSource(_options.EvaluationTimeout);
            await process.WaitForExitAsync(cts.Token);
            if (process.HasExited == false) process.Kill();
        }
        catch (Exception e)
        {
            throw new RuntimeException(e);
        }
        return NullValue.Instance;
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
