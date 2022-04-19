using System.Text;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using RedisQ.Core;
using RedisQ.Core.Lang;
using Tokens = RedisQ.Core.Lang.RedisQLLexer; 

namespace RedisQ.Cli;

internal interface IRepl
{
    Task<string> ReadSource();
}

internal class MonochromeRepl : IRepl
{
    private readonly char _terminator;

    public MonochromeRepl(char terminator) => _terminator = terminator;
    
    public Task<string> ReadSource()
    {
        var source = new StringBuilder();
        while (true)
        {
            var atBegin = source.Length == 0;
            Console.Write(atBegin ? "> " : "- ");
            var line = Console.ReadLine()!.Trim();
            if (atBegin)
            {
                if (string.IsNullOrEmpty(line)) return Task.FromResult(string.Empty);
                if (line.StartsWith("#")) return Task.FromResult(line);
            }
            source.AppendLine(line);
            if (line.EndsWith(_terminator)) return Task.FromResult(source.ToString());
        }
    }
}

internal class PrettyRepl : IRepl
{
    private readonly IPrompt _prompt;

    public PrettyRepl(char terminator, Compiler compiler)
    {
        var promptStr = new FormattedString("> ", new FormatSpan(0, 2, AnsiColor.Blue));
        _prompt = new Prompt(
            configuration: new PromptConfiguration(prompt: promptStr),
            callbacks: new LocalPromptCallbacks(compiler, terminator));
    }

    public async Task<string> ReadSource()
    {
        var response = await _prompt.ReadLineAsync();
        return response.IsSuccess
            ? response.Text
            : string.Empty;
    }
    
    private class LocalPromptCallbacks : PromptCallbacks
    {
        private static readonly KeyPress SoftEnter = new(ConsoleKey.Insert.ToKeyInfo('\0', shift: true), "\n");
        private static readonly ISet<int> Keywords = new HashSet<int>(
            new[]
            {
                Tokens.From,
                Tokens.In,
                Tokens.Let,
                Tokens.Where,
                Tokens.Select,
                Tokens.True,
                Tokens.False,
                Tokens.Null,
            });
        private static readonly ISet<int> Operators = new HashSet<int>(
            new[]
            {
                Tokens.Plus,
                Tokens.Minus,
                Tokens.Times,
                Tokens.Div,
                Tokens.Mod,
                Tokens.Lt,
                Tokens.Le,
                Tokens.Gt,
                Tokens.Ge,
                Tokens.Eq,
                Tokens.Ne,
                Tokens.Match,
                Tokens.Or,
                Tokens.And,
                Tokens.Not,
            });

        private readonly Compiler _compiler;
        private readonly char _terminator;

        public LocalPromptCallbacks(Compiler compiler, char terminator)
        {
            _compiler = compiler;
            _terminator = terminator;
        }

        protected override Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        {
            if (keyPress.ConsoleKeyInfo.Key == ConsoleKey.Enter
            && keyPress.ConsoleKeyInfo.Modifiers == default
            && string.IsNullOrWhiteSpace(text) == false && text.EndsWith(_terminator) == false)
            {
                return Task.FromResult(SoftEnter);
            }

            return Task.FromResult(keyPress);
        }

        protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        {
            if (text.StartsWith("#")) return Task.FromResult(new[] { new FormatSpan(0, text.Length, AnsiColor.BrightBlue) } as IReadOnlyCollection<FormatSpan>);
            var tokens = _compiler.Lex(text);
            var spans = tokens
                .Where(token => token.StartIndex >= 0 && token.StopIndex >= token.StartIndex)
                .Select(token => new
                {
                    token.Type,
                    token.StartIndex,
                    Length = token.StopIndex - token.StartIndex + 1,
                }).Select(t => t switch
                {
                    _ when Keywords.Contains(t.Type) => new FormatSpan(t.StartIndex, t.Length, AnsiColor.Magenta),
                    _ when Operators.Contains(t.Type) => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightMagenta),
                    _ => t.Type switch
                    {
                        Tokens.Comment => new FormatSpan(t.StartIndex, t.Length, AnsiColor.White),
                        Tokens.Integer or RedisQLLexer.Real => new FormatSpan(t.StartIndex, t.Length, AnsiColor.Yellow),
                        Tokens.StringLiteral or RedisQLLexer.CharLiteral => new FormatSpan(t.StartIndex, t.Length, AnsiColor.Green),
                        _ => new FormatSpan(t.StartIndex, t.Length, AnsiColor.Rgb(0xc0, 0xc0, 0xc0)),
                    },
                });

            return Task.FromResult(spans.ToArray() as IReadOnlyCollection<FormatSpan>);
        }
    }
}