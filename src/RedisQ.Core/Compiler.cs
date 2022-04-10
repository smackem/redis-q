using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using RedisQ.Core.Lang;

namespace RedisQ.Core;

public class Compiler
{
    public void Compile(string source)
    {
        using var sourceReader = new StringReader(source);
        var stream = new UnbufferedCharStream(sourceReader);
        var lexer = new RedisQLLexer(stream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new RedisQLParser(tokens);
        var tree = parser.expr();
        //ParseTreeWalker.Default.Walk(tree);
    }
}
