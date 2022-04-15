namespace RedisQ.Core.Runtime;

public class RuntimeException : Exception
{
    public RuntimeException(string message) 
    : base(message)
    {}
}
