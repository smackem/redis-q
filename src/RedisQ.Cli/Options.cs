using CommandLine;

// setters are required by Commandline parser
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace RedisQ.Cli;

// instantiated by Commandline parser
// ReSharper disable once ClassNeverInstantiated.Global
public record Options
{
    [Value(0, MetaName = "connectionString", Default = "localhost:6379", HelpText = "Redis connection string")]
    public string ConnectionString { get; set; } = "localhost:6379";

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    [Option('s', "simple", Required = false, Default = false, HelpText = "Use a simplistic REPL without syntax highlighting.")]
    public bool Simple { get; set; }

    [Option("chunksize", Required = false, Default = 100, HelpText = "The number of rows to print per chunk when displaying lists")]
    public int ChunkSize { get; set; } = 100;

    [Option('c', "caseSensitive", Required = false, Default = false, HelpText = "Function lookup is case-sensitive")]
    public bool IsCaseSensitive { get; set; }
}
