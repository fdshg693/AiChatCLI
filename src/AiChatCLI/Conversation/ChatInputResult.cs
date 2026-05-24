namespace AiChatCLI;

internal sealed record ChatInputResult(
    bool HandledBySlashCommand,
    string? AgentName,
    string? Reply)
{
    public static ChatInputResult SlashCommandHandled() => new(true, null, null);

    public static ChatInputResult ChatReply(string agentName, string reply) => new(false, agentName, reply);
}
