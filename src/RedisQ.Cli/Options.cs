using CommandLine;

namespace RedisQ.Cli;

public class Options
{
    [Value(0, MetaName = "connectionString", Default = "localhost:6379", HelpText = "Redis connection string")]
    public string ConnectionString { get; set; } = "localhost:6379";

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }
}