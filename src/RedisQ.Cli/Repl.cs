using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
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
    private readonly Context _ctx;

    public Repl(Options options)
    {
        _options = options;
        _functions = new FunctionRegistry(options.IsCaseSensitive == false);
        CliFunctions.Register(_functions, options);
        var redis = new RedisConnection(_options.ConnectionString);
        _ctx = Context.Root(redis, _functions);
    }

    public async Task Run()
    {
        PrintBanner(_options);
        var compiler = new Compiler();
        var printer = new ValuePrinter(_options, PromptContinue);
        ISourcePrompt sourcePrompt = _options.Simple
            ? new MonochromeSourcePrompt(Terminator)
            : new PrettySourcePrompt(Terminator, compiler, ident => _functions.TryResolve(ident));
        while (true)
        {
            var source = TrimSource(await sourcePrompt.ReadSource());
            if (string.IsNullOrEmpty(source)) continue;
            var (quit, handled) = await HandleShellCommand(source);
            if (quit) return;
            if (handled) continue;
            var value = await Interpret(compiler, source, _ctx);
            if (value == null) continue;
            await Print(printer, Console.Out, value);
            BindIt(value);
        }
    }

    /// <summary>
    /// interprets the passed script which may contain multiple commands, separated by the <see cref="Terminator"/>
    /// </summary>
    public async Task InterpretScript(string source)
    {
        var compiler = new Compiler();
        var printer = new ValuePrinter(_options, PromptContinue);
        var value = await Interpret(compiler, source, _ctx);
        if (value == null) return;
        await Print(printer, Console.Out, value);
        BindIt(value);
    }

    private void BindIt(Value value) =>
        // do not store enumerable: it does not make sense because it has already been depleted by Print
        _ctx.Bind("it", value is EnumerableValue and not ListValue
            ? NullValue.Instance
            : value);

    private static async Task<bool> PromptContinue(string prompt)
    {
        await Console.Out.WriteAsync($"{prompt}. Q to abort, any other key to continue: ");
        var key = Console.ReadKey(intercept: false);
        await Console.Out.WriteLineAsync();
        return key.Key != ConsoleKey.Q;
    }

    private async Task<(bool quit, bool handled)> HandleShellCommand(string source)
    {
        var handled = false;
        try
        {
            var match = Regex.Match(source, @"#(\w+)\b\s?(.*);?$");
            if (match.Success == false) return (false, handled);
            handled = true;
            switch (match.Groups[1].Value)
            {
                case "q": return (true, handled);
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
                case "dump":
                    PrintContext();
                    break;
                case "load" when match.Groups.Count >= 2:
                    await LoadSource(match.Groups[2].Value);
                    break;
                case "cli" when match.Groups.Count >= 2:
                    await InvokeCli(match.Groups[2].Value);
                    break;
                case "source" when match.Groups.Count >= 2:
                    PrintSource(match.Groups[2].Value);
                    break;
                case "math":
                    _options.MathMode ^= true;
                    Console.WriteLine("Math mode is {0}", _options.MathMode ? "on" : "off");
                    break;
            }
        }
        catch (Exception e)
        {
            Report(e, false);
            handled = true;
        }
        return (false, handled);
    }

    private void PrintSource(string ident)
    {
        var value = _ctx.Resolve(ident);
        if (value is FunctionValue func == false)
        {
            Console.WriteLine($"{ident} is not a function");
            return;
        }
        var parameters = string.Join(", ", func.Parameters);
        Console.WriteLine(
            $"{func.Name}({parameters}) = {func.Body.Print()}");
    }

    private async Task InvokeCli(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.CliFilePath,
            Arguments = arguments,
        };
        while (true)
        {
            try
            {
                using var process = Process.Start(psi);
                if (process == null) throw new FileNotFoundException();
                _options.CliFilePath = psi.FileName;
                await process.WaitForExitAsync();
                return;
            }
            catch (Exception e)
            {
                Console.Write($"error starting {psi.FileName}: {e.Message}{Environment.NewLine}please enter path (leave blank to cancel): ");
                psi.FileName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(psi.FileName)) return;
            }
        }
    }

    private Task LoadSource(string fileName)
    {
        var filePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Directory.GetCurrentDirectory(), fileName);
        var source = File.ReadAllText(filePath);
        return InterpretScript(source);
    }

    private void PrintContext()
    {
        string PrintValue(ValuePrinter p, Value v)
        {
            using var writer = new StringWriter();
            p.Print(v, writer).Wait();
            return writer.ToString().Trim();
        }
        var printer = new ValuePrinter(Options.Default(), null);
        var scope = _ctx.CaptureClosure().Bindings
            .Select(b => new
            {
                Name = b.Key,
                Value = PrintValue(printer, b.Value),
            });
        ConsoleTable.From(scope).Write(Format.Minimal);
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
        if (options.NoBanner) return;
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
        var compilerOptions = new CompilerOptions
        {
            ParseIntAsReal = _options.MathMode,
        };

        try
        {
            var expr = compiler.Compile(source, compilerOptions);
            var value = await expr.Evaluate(ctx).WaitAsync(timeSpan);
            return expr is LetClause && _options.QuietBindings
                ? null
                : value;
        }
        catch (Exception e) when 
            (e is TimeoutException
                or OperationCanceledException
                or CompilationException
                or RuntimeException)
        {
            Report(e, _options.Verbose);
        }

        return null;
    }

    private static void Report(Exception e, bool verbose) =>
        Console.WriteLine(verbose ? e : e.Message);

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}
