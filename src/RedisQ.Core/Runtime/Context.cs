using System.ComponentModel;
using RedisQ.Core.Redis;

namespace RedisQ.Core.Runtime;

public class Context
{
    private readonly Scope _scope = new();

    private Context(Context parent)
    {
        Parent = parent;
        Redis = parent.Redis;
    }

    private Context(RedisConnection redis, FunctionRegistry functions)
    {
        Parent = null;
        Redis = redis;
        Functions = functions;
    }

    public Context? Parent { get; }
    public RedisConnection Redis { get; }
    public FunctionRegistry Functions { get; }

    public static Context Root(RedisConnection redis, FunctionRegistry functions) => new(redis, functions);
    public static Context Inherit(Context parent) => new(parent);

    public Value? LookupBinding(string name)
    {
        for (var ctx = this; ctx != null; ctx = ctx.Parent)
        {
            var value = ctx._scope.Get(name);
            if (value != null) return value;
        }

        return null;
    }
}
