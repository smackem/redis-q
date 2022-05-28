using CommandLine;

namespace RedisQ.Cli;

internal static class Program
{
    private static Task Main(string[] args)
    {
        return CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(Execute);
    }

    private static async Task Execute(Options options)
    {
        var repl = new Repl(options);
        var runRepl = true;

        if (options.InlineSource != null)
        {
            await repl.InterpretScript(options.InlineSource);
            runRepl = options.NoExit;
            options.NoBanner = true;
        }
        else if (options.FilePath != null)
        {
            var source = await File.ReadAllTextAsync(options.FilePath);
            await repl.InterpretScript(source);
            runRepl = options.NoExit;
            options.NoBanner = true;
        }

        if (runRepl) await repl.Run();
    }
}
