namespace AiChatCLI;

internal class SlashCommandHandler
{
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ISlashCommand command)
    {
        _commands[command.Name] = command;
    }

    public IReadOnlyList<ISlashCommand> GetRegisteredCommands()
    {
        return _commands.Values
            .OrderBy(command => command.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryHandle(string input) => TryHandle(input, Console.Out);

    public bool TryHandle(string input, TextWriter output)
    {
        if (!input.StartsWith('/'))
            return false;

        var parts = input[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var name = parts[0];
        var args = parts[1..];

        if (_commands.TryGetValue(name, out var command))
        {
            command.Execute(args, output);
            return true;
        }

        output.WriteLine($"不明なコマンド: /{name}");
        output.WriteLine($"利用可能なコマンド: {string.Join(", ", _commands.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => "/" + k))}");
        output.WriteLine("詳しくは /help を参照してください。");
        return true;
    }
}
