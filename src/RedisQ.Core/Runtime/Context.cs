using RedisQ.Core.Redis;

namespace RedisQ.Core.Runtime;

public class Context
{
    private readonly Scope _scope = new();
    private readonly Context? _parent;

    private Context(Context parent)
    {
        _parent = parent;
        Redis = parent.Redis;
        Functions = parent.Functions;
    }

    private Context(IRedisConnection redis, FunctionRegistry functions)
    {
        _parent = null;
        Redis = redis;
        Functions = functions;
    }

    public IRedisConnection Redis { get; }
    public FunctionRegistry Functions { get; }

    public static Context Root(IRedisConnection redis, FunctionRegistry functions) => new(redis, functions);
    public static Context Inherit(Context parent) => new(parent);

    public Value? Resolve(string name)
    {
        for (var ctx = this; ctx != null; ctx = ctx._parent)
        {
            var value = ctx._scope.Get(name);
            if (value != null) return value;
        }

        return null;
    }

    public void Bind(string name, Value value) =>
        _scope.Set(name, value);

    public void BindAll(Scope scope) =>
        _scope.CopyFrom(scope);

    public Scope CaptureClosure()
    {
        void Recurse(Context? ctx, Scope targetScope)
        {
            if (ctx == null) return;
            Recurse(ctx._parent, targetScope);
            targetScope.CopyFrom(ctx._scope);
        }

        var closure = new Scope();
        Recurse(this, closure);
        return closure;
    }
}
