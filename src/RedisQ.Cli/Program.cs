using System.Net.Mime;
using System.Reflection;
using System.Text;
using CommandLine;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
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
        var promptStr = new FormattedString("> ", new FormatSpan(0, 2, AnsiColor.Blue));
        var prompt = new Prompt(
            configuration: new PromptConfiguration(prompt: promptStr),
            callbacks: new LocalPromptCallbacks());
        var functions = new FunctionRegistry();
        var ctx = Context.Root(redis, functions);
        var compiler = new Compiler();
        var printer = new ValuePrinter();
        while (true)
        {
            var source = await ReadSource(prompt);
            if (string.IsNullOrEmpty(source)) continue;
            if (source.StartsWith("#q")) break;
            var value = await Interpret(compiler, source, ctx);
            if (value != null) await printer.Print(value, Console.Out);
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

    private static async Task<string> ReadSource(Prompt prompt)
    {
        var response = await prompt.ReadLineAsync();
        return response.IsSuccess
            ? TrimSource(response.Text)
            : string.Empty;
    }

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);

    private class LocalPromptCallbacks : PromptCallbacks
    {
        private static readonly KeyPress SoftEnter = new(new ConsoleKeyInfo('\n', ConsoleKey.Enter, shift: true, alt: false, control: false));

        protected override Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        {
            if (keyPress.ConsoleKeyInfo.Key == ConsoleKey.Enter
            && keyPress.ConsoleKeyInfo.Modifiers == default
            && string.IsNullOrWhiteSpace(text) == false && text.EndsWith(Terminator) == false)
            {
                return Task.FromResult(new KeyPress(ConsoleKey.Insert.ToKeyInfo('\0', shift: true), "\n"));
                //return Task.FromResult(SoftEnter);
            }

            return Task.FromResult(keyPress);
        }
    }
}