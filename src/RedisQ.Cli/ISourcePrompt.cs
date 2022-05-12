using System.Text;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using RedisQ.Core;
using RedisQ.Core.Lang;
using Tokens = RedisQ.Core.Lang.RedisQLLexer; 

namespace RedisQ.Cli;

internal interface ISourcePrompt
{
    Task<string> ReadSource();
}

internal class MonochromeSourcePrompt : ISourcePrompt
{
    private readonly char _terminator;

    public MonochromeSourcePrompt(char terminator) => _terminator = terminator;
    
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

internal class PrettySourcePrompt : ISourcePrompt
{
    private readonly IPrompt _prompt;

    public PrettySourcePrompt(char terminator, Compiler compiler)
    {
        var promptStr = new FormattedString("> ", new FormatSpan(0, 2, AnsiColor.BrightBlack));
        var historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redis-q-history");
        _prompt = new Prompt(
            configuration: new PromptConfiguration(prompt: promptStr),
            callbacks: new LocalPromptCallbacks(compiler, terminator),
            persistentHistoryFilepath: historyPath);
    }

    public async Task<string> ReadSource()
    {
        var response = await _prompt.ReadLineAsync().ConfigureAwait(false);
        if (!response.IsSuccess) return string.Empty;
        if (response is KeyPressCallbackResult callbackOutput)
        {
            Console.WriteLine(Environment.NewLine + callbackOutput.Output);
            return string.Empty;
        }
        return response.Text;
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
                Tokens.Limit,
                Tokens.Offset,
                Tokens.Select,
                Tokens.True,
                Tokens.False,
                Tokens.Null,
                Tokens.OrderBy,
                Tokens.Descending,
                Tokens.Ascending,
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
                Tokens.RegexMatch,
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
            && string.IsNullOrWhiteSpace(text) == false
            && (text.EndsWith(_terminator) == false || caret < text.Length))
            {
                return Task.FromResult(SoftEnter);
            }

            return Task.FromResult(keyPress);
        }

        protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        {
            if (text.StartsWith("#")) return Task.FromResult(new[] { new FormatSpan(0, text.Length, AnsiColor.Magenta) } as IReadOnlyCollection<FormatSpan>);
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
                    _ when Keywords.Contains(t.Type) => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightCyan),
                    //_ when Operators.Contains(t.Type) => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightCyan),
                    _ => t.Type switch
                    {
                        Tokens.Integer or RedisQLLexer.Real => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightYellow),
                        Tokens.SingleQuotedString or RedisQLLexer.DoubleQuotedString => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightGreen),
                        _ => new FormatSpan(t.StartIndex, t.Length, AnsiColor.BrightWhite),
                    },
                });

            return Task.FromResult(spans.ToArray() as IReadOnlyCollection<FormatSpan>);
        }
    }
}