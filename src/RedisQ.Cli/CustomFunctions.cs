using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal static class CustomFunctions
{
    public static void Bind(Context ctx)
    {
        const string dirIdent = "dir";
        var dirFunction = new FunctionValue(dirIdent, Array.Empty<string>(),
            new FunctionInvocationExpr("SCAN", new[] {new LiteralExpr(new StringValue("*"))}));
        ctx.Bind(dirIdent, dirFunction);
    }
}
