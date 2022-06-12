namespace RedisQ.Core;

public class CompilerOptions
{
    public static readonly CompilerOptions Default = new();

    public bool ParseIntAsReal { get; init; }
}
