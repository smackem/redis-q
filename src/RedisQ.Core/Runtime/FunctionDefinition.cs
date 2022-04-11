namespace RedisQ.Core.Runtime;

public class FunctionDefinition
{
    private readonly Func<Context, Value[], Task<Value>> _invocation;

    public FunctionDefinition(string name, int parameterCount, Func<Context, Value[], Task<Value>> invocation) =>
        (Name, ParameterCount, _invocation) = (name, parameterCount, invocation);
    
    public string Name { get; }
    public int ParameterCount { get; }

    public Task<Value> Invoke(Context ctx, Value[] arguments) => _invocation(ctx, arguments);
}