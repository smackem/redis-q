using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using ConsoleTables;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
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
    private readonly ReplCommandRegistry _replCommands;

    public Repl(Options options)
    {
        _options = options;
        _functions = new FunctionRegistry(options.IsCaseSensitive == false);
        CliFunctions.Register(_functions, options);
        var redis = new RedisConnection(_options.ConnectionString);
        _ctx = Context.Root(redis, _functions);
        _replCommands = new ReplCommandRegistry(PrintHelp);
        RegisterReplCommands();
        BindCustomFunctions();
    }

    public async Task Run()
    {
        if (_options.NoBanner is false) PrintBanner();
        var compiler = new Compiler();
        var printer = new ValuePrinter(_options, PromptContinue);
        ISourcePrompt sourcePrompt = _options.Simple
            ? new MonochromeSourcePrompt(Terminator)
            : new PrettySourcePrompt(Terminator, compiler, ident => _functions.TryResolve(ident));
        while (true)
        {
            var source = TrimSource(await sourcePrompt.ReadSource());
            if (string.IsNullOrEmpty(source)) continue;
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (await HandleShellCommand(source))
            {
                case ShellCommandInvocation.QuitRequested: return;
                case ShellCommandInvocation.Handled: continue;
            }

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

    private void BindCustomFunctions()
    {
        const string dirIdent = "dir";
        var dirFunction = new FunctionValue(dirIdent, Array.Empty<string>(),
            new FunctionInvocationExpr("SCAN", new[] {new LiteralExpr(new StringValue("*"))}));
        _ctx.Bind(dirIdent, dirFunction);
    }

    private static async Task<bool> PromptContinue(string prompt)
    {
        await Console.Out.WriteAsync($"{prompt}. Q to abort, any other key to continue: ");
        var key = Console.ReadKey(intercept: false);
        await Console.Out.WriteLineAsync();
        return key.Key != ConsoleKey.Q;
    }

    private async Task<ShellCommandInvocation> HandleShellCommand(string source)
    {
        try
        {
            var match = Regex.Match(source, @"#(\w+)\b\s?(.*);?$");
            if (match.Success == false) return ShellCommandInvocation.Unhandled;
            var quit = await _replCommands.InvokeCommand(match.Groups[1].Value, match.Groups[2].Value);
            return quit
                ? ShellCommandInvocation.QuitRequested
                : ShellCommandInvocation.Handled;
        }
        catch (Exception e)
        {
            Report(e, false);
            return ShellCommandInvocation.Handled;
        }
    }

    private enum ShellCommandInvocation
    {
        Unhandled,
        Handled,
        QuitRequested,
    }

    private void RegisterReplCommands()
    {
        _replCommands.Register(new ReplCommand("pwd", false,
            _ =>
            {
                Console.WriteLine(Environment.CurrentDirectory);
                return Task.CompletedTask;
            },
            "print the current working directory"));
        _replCommands.Register(new ReplCommand("ls", false,
            _ =>
            {
                PrintFiles();
                return Task.CompletedTask;
            },
            "list file system entries in current working directory"));
        _replCommands.Register(new ReplCommand("cd", true,
            arg =>
            {
                ChangeDirectory(arg);
                return Task.CompletedTask;
            },
            "change working directory"));
        _replCommands.Register(new ReplCommand("dump", false,
            _ =>
            {
                PrintContext();
                return Task.CompletedTask;
            },
            "print current context (all top-level bindings)"));
        _replCommands.Register(new ReplCommand("load", true,
            LoadSource,
            "load and interpret script from given file"));
        _replCommands.Register(new ReplCommand("cli", null,
            InvokeCli,
            "invoke cli executable and pass the given arguments"));
        _replCommands.Register(new ReplCommand("source", true,
            arg =>
            {
                PrintSource(arg);
                return Task.CompletedTask;
            },
            "print the definition of the given function"));
        _replCommands.Register(new ReplCommand("math", null,
            arg =>
            {
                bool? mathMode = arg switch
                {
                    "on" => true,
                    "off" => false,
                    _ => null,
                };
                if (mathMode != null) _options.MathMode = mathMode.Value;
                Console.WriteLine("Math mode is {0}", _options.MathMode ? "on" : "off");
                return Task.CompletedTask;
            },
            "switch math mode 'on', 'off' or display its current value. in math mode, all number literals are real"));
        _replCommands.Register(new ReplCommand("r", true,
            InvokeRun,
            "run the commandline given as argument"));
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

    private static async Task InvokeRun(string arguments)
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + arguments,
            }
            : new ProcessStartInfo
            {
                FileName = "sh",
                ArgumentList = { "-c", arguments },
            };
        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new FileNotFoundException();
            await process.WaitForExitAsync();
        }
        catch (Exception e)
        {
            Console.Write($"error starting {psi.FileName}: {e.Message}");
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
        PrintBanner();

        Console.WriteLine("Visit {0} for documentation", Emphasis("https://github.com/smackem/redis-q"));
        Console.WriteLine("Built-in functions:");
        Console.WriteLine();

        var functions = _functions
            .Select(f => new
            {
                FunctionName = f.Name,
                Signature = f.HelpText,
            })
            .OrderBy(f => f.FunctionName, StringComparer.Ordinal);

        ConsoleTable.From(functions).Write(Format.Minimal);
    }

    private static void ChangeDirectory(string directory)
    {
        directory = Regex.Replace(directory, @"^\~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Directory.SetCurrentDirectory(
            Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(Directory.GetCurrentDirectory(), directory));
        Console.WriteLine(Environment.CurrentDirectory);
    }

    private static void PrintFiles()
    {
        var baseDir = Environment.CurrentDirectory;
        Console.WriteLine($"  directory: {baseDir}");
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

    private void PrintBanner()
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        Console.WriteLine("***** {0} v{1}", Title("redis-q"), version);
        Console.WriteLine("redis @ {0}", Emphasis(_options.ConnectionString));
        Console.WriteLine("terminate expressions with {0}", Input(";"));
        Console.WriteLine("enter {0} for help...", Input("#h;"));
        Console.WriteLine();
    }

    private string Emphasis(string s) =>
        _options.Simple
            ? s
            : AnsiColor.Yellow.GetEscapeSequence() + s + AnsiEscapeCodes.Reset;

    private string Input(string s) =>
        _options.Simple
            ? s
            : AnsiColor.Magenta.GetEscapeSequence() + s + AnsiEscapeCodes.Reset;

    private string Title(string s) =>
        _options.Simple
            ? s
            : AnsiColor.Cyan.GetEscapeSequence() + s + AnsiEscapeCodes.Reset;

    private static void Report(Exception e, bool verbose) =>
        Console.WriteLine(verbose ? e : e.Message);

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}
