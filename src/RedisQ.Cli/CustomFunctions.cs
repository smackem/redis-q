using RedisQ.Core;
using RedisQ.Core.Runtime;

namespace RedisQ.Cli;

internal static class CustomFunctions
{
    public static void Bind(Context ctx)
    {
        const string dirIdent = "dir";
        ctx.Bind(dirIdent, CreateFunctionDir(dirIdent));

        const string lsIdent = "ls";
        ctx.Bind(lsIdent, CreateFunctionLs(lsIdent));
    }

    private static FunctionValue CreateFunctionDir(string ident) =>
        new(ident, Array.Empty<string>(),
            new FunctionInvocationExpr("SCAN", new[] {new LiteralExpr(new StringValue("*"))}));

    private static FunctionValue CreateFunctionLs(string ident) =>
        new(ident, new[] {"sub"},
            new Compiler().Compile(@"
let pat = from c in chars(sub) select '[' + lower(c) + upper(c) + ']' |> join('') in
scan('*' + pat + '*');"));
}
