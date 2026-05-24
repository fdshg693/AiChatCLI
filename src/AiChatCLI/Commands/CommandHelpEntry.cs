namespace AiChatCLI;

internal sealed record CommandHelpEntry(
    string CommandPath,
    string Description,
    IReadOnlyList<string> Usages);
