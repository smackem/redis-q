namespace RedisQ.Core.Runtime;

public class FunctionDefinition
{
    private readonly Func<Context, Value[], Task<Value>> _invocation;

    public FunctionDefinition(string name, int parameterCount, Func<Context, Value[], Task<Value>> invocation,
        string helpText) =>
        (Name, ParameterCount, HelpText, _invocation) = (name, parameterCount, helpText, invocation);

    public string Name { get; }
    public int ParameterCount { get; }
    public string HelpText { get; }

    public Task<Value> Invoke(Context ctx, Value[] arguments) => _invocation(ctx, arguments);
}
