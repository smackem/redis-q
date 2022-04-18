namespace RedisQ.Core;

public class CompilationException : Exception
{
    public CompilationException(string message)
    : base(message)
    {}

    public CompilationException(Exception innerException)
        : base(innerException.Message, innerException)
    {}
}
