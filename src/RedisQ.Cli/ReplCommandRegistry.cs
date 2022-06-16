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
                if (_commands.TryGetValue(command, out var cmd))
                {
                    await cmd.Action(arguments);
                }
                else
                {
                    Console.WriteLine($"unknown shell command '{command}'. enter #h; for help.");
                }
                break;
            }
        }
        return false;
    }

    private void PrintHelp()
    {
        _printHelp();
    }
}

internal record ReplCommand(
    string Name,
    bool HasParameter,
    Func<string, Task> Action,
    string HelpText);
