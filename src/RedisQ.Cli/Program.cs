using System.Reflection;
using System.Text.RegularExpressions;
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
        CliFunctions.Register(functions);
        var ctx = Context.Root(redis, functions);
        var compiler = new Compiler();
        var printer = new ValuePrinter(options);
        IRepl repl = options.Simple
            ? new MonochromeRepl(Terminator)
            : new PrettyRepl(Terminator, compiler);
        while (true)
        {
            var source = TrimSource(await repl.ReadSource());
            if (string.IsNullOrEmpty(source)) continue;
            if (HandleShellCommand(source, out var handled)) return;
            if (handled) continue;
            var value = await Interpret(compiler, source, ctx, options);
            if (value == null) continue;
            await Print(printer, Console.Out, value, options);
            // do not store enumerable: it does not make sense because it has already been depleted by Print
            ctx.Bind("it", value is EnumerableValue and not ListValue
                ? NullValue.Instance
                : value);
        }
    }

    private static bool HandleShellCommand(string source, out bool handled)
    {
        handled = false;
        var match = Regex.Match(source, @"#(\w+)\b\s?(.*);?$");
        if (match.Success == false) return false;
        handled = true;
        switch (match.Groups[1].Value)
        {
            case "q": return true;
            case "pwd":
                Console.WriteLine(Environment.CurrentDirectory);
                break;
            case "ls":
                var entries = Directory.EnumerateFileSystemEntries(Environment.CurrentDirectory);
                foreach (var path in entries)
                {
                    Console.WriteLine(Path.GetFileName(path));
                }
                break;
        }
        return false;
    }

    private static void PrintBanner(Options options)
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        Console.WriteLine($"***** redis-q v{version}");
        Console.WriteLine($"redis @ {options.ConnectionString}");
        Console.WriteLine("terminate expressions with ;");
        Console.WriteLine("enter #q; to quit...");
        Console.WriteLine();
    }

    private static async Task Print(ValuePrinter printer, TextWriter writer, Value value, Options options)
    {
        try
        {
            await printer.Print(value, writer);
        }
        catch (Exception e)
        {
            Report(e, options.Verbose);
        }
    }

    private static async Task<Value?> Interpret(Compiler compiler, string source, Context ctx, Options options)
    {
        try
        {
            var expr = compiler.Compile(source);
            return await expr.Evaluate(ctx);
        }
        catch (CompilationException e)
        {
            Report(e, options.Verbose);
        }
        catch (RuntimeException e)
        {
            // Evaluate translates all exceptions into RuntimeExceptions, so
            // we don't need a catch-all clause
            Report(e, options.Verbose);
        }
        return null;
    }

    private static void Report(Exception e, bool verbose) =>
        Console.WriteLine(verbose ? e : e.Message);

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}