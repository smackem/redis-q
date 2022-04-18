namespace RedisQ.Core.Runtime;

public class RuntimeException : Exception
{
    public RuntimeException(string message) 
    : base(message)
    {}

    public RuntimeException(Exception innerException)
    : base(innerException.Message, innerException)
    {}
}
