using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using ConsoleTables;
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
        var printer = new ValuePrinter(options, PromptContinue);
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

    private static async Task<bool> PromptContinue(string prompt)
    {
        await Console.Out.WriteAsync($"{prompt}. Q to abort, any other key to continue: ");
        var key = Console.ReadKey(intercept: false);
        await Console.Out.WriteLineAsync();
        return key.Key == ConsoleKey.Q;
    }

    private static bool HandleShellCommand(string source, out bool handled)
    {
        handled = false;
        try
        {
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
                    PrintFiles();
                    break;
                case "cd" when match.Groups.Count >= 2:
                    ChangeDirectory(match.Groups[2].Value);
                    break;
            }
        }
        catch (Exception e)
        {
            Report(e, false);
            handled = true;
        }
        return false;
    }

    private static void ChangeDirectory(string directory)
    {
        Directory.SetCurrentDirectory(
            Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(Directory.GetCurrentDirectory(), directory));
        Console.WriteLine(Environment.CurrentDirectory);
    }

    private static void PrintFiles()
    {
        var baseDir = Environment.CurrentDirectory;
        var files = Directory.GetDirectories(baseDir)
            .OrderBy(path => path)
            .Select(path => new
            {
                LastModified = "<DIR>",
                Name = Path.GetFileName(path),
            }).Concat(
                Directory.GetFiles(baseDir)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastAccessTime)
                    .Select(file => new
                    {
                        LastModified = file.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Name = file.Name,
                    }));
        ConsoleTable.From(files).Write(Format.Minimal);
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
            // Evaluate() translates all exceptions into RuntimeExceptions, so
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