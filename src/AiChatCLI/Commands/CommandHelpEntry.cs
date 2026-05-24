namespace AiChatCLI;

public sealed record CommandHelpEntry(
    string CommandPath,
    string Description,
    IReadOnlyList<string> Usages);
