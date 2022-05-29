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
        var scriptMode = false;
        var oldQuietBindings = options.QuietBindings;

        if (options.FilePath != null)
        {
            options.QuietBindings = true;
            var source = await File.ReadAllTextAsync(options.FilePath);
            await repl.InterpretScript(source);
            scriptMode = true;
        }

        if (options.InlineSource != null)
        {
            options.QuietBindings = true;
            await repl.InterpretScript(options.InlineSource);
            scriptMode = true;
        }

        if (scriptMode)
        {
            runRepl = options.NoExit;
            options.NoBanner = true;
            options.QuietBindings = oldQuietBindings;
        }

        if (runRepl) await repl.Run();
    }
}
