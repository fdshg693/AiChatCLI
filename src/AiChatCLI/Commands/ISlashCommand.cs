namespace AiChatCLI;

internal interface ISlashCommand
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<CommandHelpEntry> HelpEntries { get; }
    void Execute(string[] args, TextWriter output);
}
