namespace RedisQ.Core.Runtime;

public class Scope
{
    private readonly IDictionary<string, Value> _bindings = new Dictionary<string, Value>();

    public IReadOnlyList<KeyValuePair<string, Value>> Bindings => _bindings.ToArray();

    public void CopyFrom(Scope source)
    {
        foreach (var (key, value) in source.Bindings)
        {
            Set(key, value);
        }
    }

    public Value? Get(string name) => _bindings.TryGetValue(name, out var value) ? value : null;

    public void Set(string name, Value value) => _bindings[name] = value;
}
