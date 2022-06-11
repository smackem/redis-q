using CommandLine;

// setters are required by Commandline parser
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace RedisQ.Cli;

// instantiated by Commandline parser
// ReSharper disable once ClassNeverInstantiated.Global
public record Options
{
    public static Options Default() => new();

    [Value(0, MetaName = "connection-string", Default = "localhost:6379", HelpText = "Redis connection string")]
    public string ConnectionString { get; set; } = "localhost:6379";

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    [Option('s', "simple", Required = false, Default = false, HelpText = "Use a simplistic REPL without syntax highlighting.")]
    public bool Simple { get; set; }

    [Option("chunk-size", Required = false, Default = 100, HelpText = "The number of rows to print per chunk when displaying lists")]
    public int ChunkSize { get; set; } = 100;

    [Option('c', "case-sensitive", Required = false, Default = false, HelpText = "Function lookup is case-sensitive")]
    public bool IsCaseSensitive { get; set; }

    [Option('t', "timeout", Required = false, Default = 1000, HelpText = "The maximum duration of an evaluation in milliseconds")]
    public int EvaluationTimeout { get; set; }

    [Option('f', "file", Required = false, Default = null, HelpText = "Interpret input from file and quit. If --eval is also passed, process the inline source after the file.")]
    public string? FilePath { get; set; }
    
    [Option('e', "eval", Required = false, Default = null, HelpText = "Interpret passed source code and quit. If --file is also passed, process the file first.")]
    public string? InlineSource { get; set; }
    
    [Option("no-exit", Required = false, Default = false, HelpText = "Don't quit after evaluating source code passed with --file or --eval")]
    public bool NoExit { get; set; }
    
    [Option("no-banner", Required = false, Default = false, HelpText = "Don't print welcome message on startup")]
    public bool NoBanner { get; set; }
    
    [Option('q', "quiet-bindings", Required = false, Default = false, HelpText = "Don't print value of bindings ('let' commands) on creation")]
    public bool QuietBindings { get; set; }

    [Option("cli", Required = false, Default = "redis-cli", HelpText = "The path to the executable to start with the REPL command '#cli <ARGS>'")]
    public string CliFilePath { get; set; } = "redis-cli";
}
