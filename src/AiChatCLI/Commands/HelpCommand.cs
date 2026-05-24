namespace AiChatCLI.Commands;

internal class HelpCommand : ISlashCommand
{
    private static readonly CommandHelpEntry[] HelpEntryDefinitions =
    [
        new(
            "/help",
            "利用可能なコマンドと命名規約を表示",
            ["/help"])
    ];

    private readonly SlashCommandHandler _slashCommandHandler;

    public HelpCommand(SlashCommandHandler slashCommandHandler)
    {
        _slashCommandHandler = slashCommandHandler;
    }

    public string Name => "help";
    public string Description => "利用可能なコマンドと命名規約を表示";
    public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntryDefinitions;

    public void Execute(string[] args, TextWriter output)
    {
        output.WriteLine("--- コマンドヘルプ ---");
        output.WriteLine($"基本文法: {CommandConventions.GroupedGrammar}");
        output.WriteLine($"単独リソース: {CommandConventions.StandaloneGrammar}");
        output.WriteLine();

        foreach (var entry in _slashCommandHandler.GetRegisteredCommands().SelectMany(command => command.HelpEntries))
        {
            CommandConventions.ShowHelpEntry(entry, output);
            output.WriteLine();
        }

        output.WriteLine();
        output.WriteLine("将来追加する機能も、原則として /<resource> <subresource> <action> に揃えます。");
        output.WriteLine("----------------------");
    }
}
