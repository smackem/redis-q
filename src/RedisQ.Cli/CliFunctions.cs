using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

public static class CliFunctions
{
    public static void Register(FunctionRegistry registry)
    {
        registry.Register(new FunctionDefinition("clip", 1, FuncClip));
        registry.Register(new FunctionDefinition("save", 2, FuncSave));
    }

    private static Task<Value> FuncClip(Context ctx, Value[] arguments)
    {
        throw new NotImplementedException();
    }

    private static Task<Value> FuncSave(Context ctx, Value[] arguments)
    {
        throw new NotImplementedException();
    }
}
