using Antlr4.Runtime;
using RedisQ.Core.Lang;
using RedisQ.Core.Runtime;

namespace RedisQ.Core;

public class Compiler
{
    public Expr Compile(string source)
    {
        try
        {
            var errorMessages = new List<string>();
            var tokens = CreateTokenStream(source, errorMessages);
            var parser = new RedisQLParser(tokens);
            var parserErrorListener = new ParserErrorListener(errorMessages);
            parser.AddErrorListener(parserErrorListener);
            var tree = parser.main();
            return errorMessages.Count == 0
                ? tree.Accept(new Emitter())
                : throw new CompilationException(JoinErrorMessages(errorMessages));
        }
        catch (CompilationException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new CompilationException(e);
        }
    }

    public IList<IToken> Lex(string source)
    {
        var errorMessages = new List<string>();
        var tokens = CreateTokenStream(source, errorMessages);
        tokens.Fill();
        return errorMessages.Count == 0
            ? tokens.GetTokens()
            : throw new CompilationException(JoinErrorMessages(errorMessages));
    }

    private static string JoinErrorMessages(IEnumerable<string> messages) => string.Join('\n', messages);

    private static CommonTokenStream CreateTokenStream(string source, List<string> outErrorMessages)
    {
        using var sourceReader = new StringReader(source);
        var errorMessages = new List<string>();
        var stream = new AntlrInputStream(sourceReader);
        var lexer = new RedisQLLexer(stream);
        var lexerErrorListener = new LexerErrorListener(errorMessages);
        lexer.AddErrorListener(lexerErrorListener);
        return new CommonTokenStream(lexer);
    }

    private record LexerErrorListener(IList<string> ErrorMessageSink) : IAntlrErrorListener<int>
    {
        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) =>
            ErrorMessageSink.Add($"Lexer error ({line}:{charPositionInLine}): {msg}");
    }

    private record ParserErrorListener(IList<string> ErrorMessageSink) : IAntlrErrorListener<IToken>
    {
        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) =>
            ErrorMessageSink.Add($"Compiler error ({line}:{charPositionInLine}): {msg}");
    }
}
