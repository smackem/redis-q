using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal class ValuePrinter
{
    public void Print(Value value, TextWriter writer)
    {
        writer.WriteLine(value.ToString());
    }
}
