using System.Diagnostics;
using Antlr4.Runtime;
using RedisQ.Core.Lang;
using RedisQ.Core.Runtime;

namespace RedisQ.Core;

public class Compiler
{
    /// <summary>
    /// compiles the source code, throwing an exception when compilation errors occur
    /// </summary>
    public Expr Compile(string source, CompilerOptions? options = null)
    {
        options ??= CompilerOptions.Default;
        try
        {
            var errorMessages = new List<string>();
            var tokens = CreateTokenStream(PrepareSource(source), errorMessages);
            var parser = new RedisQLParser(tokens);
            var parserErrorListener = new ParserErrorListener(errorMessages);
            parser.AddErrorListener(parserErrorListener);
            var tree = parser.main();
            return errorMessages.Count == 0
                ? tree.Accept(new Emitter(options.ParseIntAsReal))
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

    /// <summary>
    /// tokenizes the source code as far as possible and does not throw exceptions
    /// </summary>
    public IList<IToken> Lex(string source)
    {
        var errorMessages = new List<string>();
        var tokens = CreateTokenStream(PrepareSource(source), errorMessages);
        tokens.Fill();
        if (errorMessages.Count > 0) Trace.WriteLine(JoinErrorMessages(errorMessages));
        return tokens.GetTokens();
    }

    private static string PrepareSource(string source) =>
        source.Trim() switch
        {
            var s when s.EndsWith(';') => s,
            var s => s + ';',
        };

    private static string JoinErrorMessages(IEnumerable<string> messages) => string.Join(Environment.NewLine, messages);

    private static CommonTokenStream CreateTokenStream(string source, IList<string> outErrorMessages)
    {
        using var sourceReader = new StringReader(source);
        var stream = new AntlrInputStream(sourceReader);
        var lexer = new RedisQLLexer(stream);
        var lexerErrorListener = new LexerErrorListener(outErrorMessages);
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
