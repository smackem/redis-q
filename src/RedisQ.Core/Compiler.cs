using Antlr4.Runtime;
using RedisQ.Core.Lang;

namespace RedisQ.Core;

public class Compiler
{
    /*
        final CharStream input = CharStreams.fromString(source);
        final JobotwarV1Lexer lexer = new JobotwarV1Lexer(input);
        final ErrorListener errorListener = new ErrorListener();
        lexer.addErrorListener(errorListener);
        final CommonTokenStream tokens = new CommonTokenStream(lexer);
        final JobotwarV1Parser parser = new JobotwarV1Parser(tokens);
        parser.addErrorListener(errorListener);
        final JobotwarV1Parser.ProgramContext tree = parser.program();
        final Emitter emitter = new Emitter();
        final EmittingListenerV1 listener = new EmittingListenerV1(emitter);
        ParseTreeWalker.DEFAULT.walk(listener, tree);
        outErrors.addAll(errorListener.errors);
    */

    public void Compile(string source)
    {
        using var sourceReader = new StringReader(source);
        var stream = new UnbufferedCharStream(sourceReader);
        var lexer = new RedisQLLexer(stream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new RedisQLParser(tokens);
        var tree = parser.expression();
        //ParseTreeWalker.Default.Walk(tree);
    }
}
