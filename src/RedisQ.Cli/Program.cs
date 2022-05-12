using CommandLine;

namespace RedisQ.Cli;

internal static class Program
{
    private static Task Main(string[] args)
    {
        return CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(options => new Repl(options).Run());
    }
}