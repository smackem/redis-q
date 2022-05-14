using System.Reflection;
using System.Text.RegularExpressions;
using ConsoleTables;
using RedisQ.Core;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

public class Repl
{
    private const char Terminator = ';';
    private readonly FunctionRegistry _functions;
    private readonly Options _options;

    public Repl(Options options)
    {
        _options = options;
        _functions = new FunctionRegistry(options.IsCaseSensitive == false);
        CliFunctions.Register(_functions);
    }

    public async Task Run()
    {
        PrintBanner(_options);
        var redis = new RedisConnection(_options.ConnectionString);
        var ctx = Context.Root(redis, _functions);
        var compiler = new Compiler();
        var printer = new ValuePrinter(_options, PromptContinue);
        ISourcePrompt sourcePrompt = _options.Simple
            ? new MonochromeSourcePrompt(Terminator)
            : new PrettySourcePrompt(Terminator, compiler);
        while (true)
        {
            var source = TrimSource(await sourcePrompt.ReadSource());
            if (string.IsNullOrEmpty(source)) continue;
            if (HandleShellCommand(source, out var handled)) return;
            if (handled) continue;
            var value = await Interpret(compiler, source, ctx);
            if (value == null) continue;
            await Print(printer, Console.Out, value);
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
        return key.Key != ConsoleKey.Q;
    }

    private bool HandleShellCommand(string source, out bool handled)
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
                case "h" or "help":
                    PrintHelp();
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

    private void PrintHelp()
    {
        var functions = _functions
            .Select(f =>
                new
                {
                    FunctionName = f.Name,
                    Signature = f.HelpText,
                })
            .OrderBy(f => f.FunctionName, StringComparer.Ordinal);

        ConsoleTable.From(functions).Write(Format.Minimal);
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

    private async Task Print(ValuePrinter printer, TextWriter writer, Value value)
    {
        try
        {
            await printer.Print(value, writer);
        }
        catch (Exception e)
        {
            Report(e, _options.Verbose);
        }
    }

    private async Task<Value?> Interpret(Compiler compiler, string source, Context ctx)
    {
        var timeSpan = TimeSpan.FromMilliseconds(_options.EvaluationTimeout);
        try
        {
            var expr = compiler.Compile(source);
            return await expr.Evaluate(ctx).WaitAsync(timeSpan);
        }
        catch (OperationCanceledException e)
        {
            Report(e, _options.Verbose);
        }
        catch (CompilationException e)
        {
            Report(e, _options.Verbose);
        }
        catch (RuntimeException e)
        {
            // Evaluate() translates all exceptions into RuntimeExceptions, so
            // we don't need a catch-all clause
            Report(e, _options.Verbose);
        }
        return null;
    }

    private static void Report(Exception e, bool verbose) =>
        Console.WriteLine(verbose ? e : e.Message);

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}
