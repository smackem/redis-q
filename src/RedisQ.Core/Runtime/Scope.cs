namespace RedisQ.Core.Runtime;

public class Scope
{
    private readonly IDictionary<string, Value> _bindings = new Dictionary<string, Value>();

    public Value? Get(string name) => _bindings.TryGetValue(name, out var value) ? value : null;

    public void Set(string name, Value value) => _bindings[name] = value;
}