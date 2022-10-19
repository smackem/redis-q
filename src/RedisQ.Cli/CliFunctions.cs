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
        registry.Register(new("clip", 1, FuncClip, "(value: any) -> value"));
        registry.Register(new("save", 2, FuncSave, "(path: string, value: any) -> value"));
        registry.Register(new("trace", 1, FuncTrace, "(value: any) -> value"));
        registry.Register(new("cli", 1, instance.FuncCli, "(value: any) -> string"));
        registry.Register(new("load", 1, FuncLoad, "(path: string) -> value"));
    }

    private async Task<Value> FuncCli(Context ctx, Value[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.CliFilePath,
            Arguments = arguments[0].AsString(),
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new FileNotFoundException();
            var cts = new CancellationTokenSource((int) (_options.EvaluationTimeout * 0.9));
            process.StandardInput.Close();
            var output = await process.StandardOutput.ReadToEndAsync().WaitAsync(cts.Token);
            if (process.HasExited == false) process.Kill();
            return new StringValue(output);
        }
        catch (Exception e)
        {
            throw new RuntimeException(e);
        }
    }

    private static Task<Value> FuncTrace(Context ctx, Value[] arguments)
    {
        var input = arguments[0];
        var timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.WriteLine($"{timeStr} {input.AsString()}");
        return Task.FromResult(input);
    }

    private static async Task<Value> FuncClip(Context ctx, Value[] arguments)
    {
        var options = Options.Default();
        var printer = new ValuePrinter(options, null);
        var input = arguments[0];
        await using var writer = new StringWriter();
        await printer.Print(input, writer);
        var str = writer.ToString();
        await ClipboardService.SetTextAsync(str);
        return input;
    }

    private static async Task<Value> FuncSave(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue path == false) throw new RuntimeException($"save({arguments[0]}): incompatible operand, string expected");
        await using var stream = File.Create(path.Value);
        var input = arguments[1];
        if (input is RedisValue value)
        {
            await stream.WriteAsync(value.Value);
        }
        else
        {
            await using var writer = new StreamWriter(stream);
            var options = Options.Default(); // override options
            var printer = new ValuePrinter(options, null);
            await printer.Print(input, writer);
        }
        return input;
    }

    private static async Task<Value> FuncLoad(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue path == false) throw new RuntimeException($"load({arguments[0]}): incompatible operand, string expected");
        return new RedisValue(await File.ReadAllBytesAsync(path.Value));
    }
}
