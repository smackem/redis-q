using System.Reflection;
using CommandLine;
using RedisQ.Core;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal static class Program
{
    private const char Terminator = ';';

    private static Task Main(string[] args)
    {
        return CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(RunRepl);
    }

    private static async Task RunRepl(Options options)
    {
        PrintBanner(options);
        var redis = new RedisConnection(options.ConnectionString);
        var functions = new FunctionRegistry();
        var ctx = Context.Root(redis, functions);
        var compiler = new Compiler();
        var printer = new ValuePrinter();
        IRepl repl = options.Monochrome
            ? new MonochromeRepl(Terminator)
            : new PrettyRepl(Terminator, compiler);
        while (true)
        {
            var source = TrimSource(await repl.ReadSource());
            if (string.IsNullOrEmpty(source)) continue;
            if (source.StartsWith("#q")) break;
            var value = await Interpret(compiler, source, ctx);
            if (value == null) continue;
            ctx.Bind("it", value);
            await printer.Print(value, Console.Out);
        }
    }

    private static void PrintBanner(Options options)
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        Console.WriteLine($"***** redis-q v{version}");
        Console.WriteLine($"redis @ {options.ConnectionString}");
        Console.WriteLine("terminate expressions with ;");
        Console.WriteLine("enter #q; to exit...");
    }

    private static async Task<Value?> Interpret(Compiler compiler, string source, Context ctx)
    {
        try
        {
            var expr = compiler.Compile(source);
            return await expr.Evaluate(ctx);
        }
        catch (CompilationException e)
        {
            Console.WriteLine(e);
        }
        catch (RuntimeException e)
        {
            Console.WriteLine(e);
        }
        return null;
    }

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}