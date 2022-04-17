using System.Text;
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
        var redis = new RedisConnection(options.ConnectionString);
        var functions = new FunctionRegistry();
        var ctx = Context.Root(redis, functions);
        var compiler = new Compiler();
        var printer = new ValuePrinter();
        while (true)
        {
            var source = ReadSource();
            if (string.IsNullOrEmpty(source)) continue;
            if (source.StartsWith("#q")) break;
            var value = await Interpret(compiler, source, ctx);
            if (value != null) await printer.Print(value, Console.Out);
        }
    }

    private static async Task<Value?> Interpret(Compiler compiler, string source, Context ctx)
    {
        try
        {
            var expr = compiler.Compile(source);
            if (expr == null) throw new CompilationException("TODO: implement compilation error reporting");
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

    private static string ReadSource()
    {
        var source = new StringBuilder();
        while (true)
        {
            var atBegin = source.Length == 0;
            Console.Write(atBegin ? "> " : ": ");
            var line = Console.ReadLine()!.Trim();
            if (atBegin)
            {
                if (string.IsNullOrEmpty(line)) return string.Empty;
                if (line.StartsWith("#")) return line;
            }
            source.AppendLine(line);
            if (line.EndsWith(Terminator)) return TrimSource(source.ToString());
        }
    }

    private static string TrimSource(string source) =>
        source.TrimEnd('\r', '\n', ' ', '\t', Terminator);
}