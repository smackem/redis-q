using ConsoleTables;

namespace RedisQ.Cli;

internal class ReplCommandRegistry
{
    private readonly IDictionary<string, ReplCommand> _commands =
        new Dictionary<string, ReplCommand>();

    private readonly Action _printHelp;

    public ReplCommandRegistry(Action printHelp) =>
        _printHelp = printHelp;
    
    public void Register(ReplCommand command) =>
        _commands.Add(command.Name, command);

    public async Task<bool> InvokeCommand(string command, string arguments)
    {
        switch (command)
        {
            case "q": return true;
            case "h" or "help":
                PrintHelp();
                break;
            default:
            {
                switch (_commands.TryGetValue(command, out var cmd), cmd, arguments)
                {
                    case (true, { HasParameter: true}, not "")
                    or (true, { HasParameter: null}, _):
                        await cmd.Action(arguments);
                        break;
                    case (true, { HasParameter: false}, _):
                        await cmd.Action(string.Empty);
                        break;
                    case (true, { HasParameter: true}, ""):
                        Console.WriteLine(cmd.RenderInvocation(true));
                        break;
                    case (false, _, _):
                        Console.WriteLine($"unknown shell command '{command}'. enter #h; for help.");
                        break;
                }
                break;
            }
        }
        return false;
    }

    private void PrintHelp()
    {
        _printHelp();

        Console.WriteLine("Available shell commands:");
        Console.WriteLine();

        var commands =
            _commands.Values
                .Select(
                    c => new
                    {
                        Invocation = c.RenderInvocation(false),
                        Description = c.HelpText,
                    })
                .OrderBy(c => c.Invocation, StringComparer.Ordinal);

        ConsoleTable.From(commands).Write(Format.Minimal);
    }
}

internal record ReplCommand(
    string Name,
    bool? HasParameter,
    Func<string, Task> Action,
    string HelpText)
{
    public string RenderInvocation(bool includeHelpText) =>
        // ReSharper disable once UseStringInterpolationWhenPossible
        string.Format(
            "#{0}{1};{2}",
            Name,
            HasParameter switch
            {
                true => " ARG",
                null => " [ARG]",
                _ => string.Empty,
            },
            includeHelpText ? " " + HelpText : string.Empty);
}
