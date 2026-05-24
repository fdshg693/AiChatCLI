namespace AiChatCLI;

internal sealed record ThreadSnapshot(
    string ThreadId,
    string CurrentAgentName,
    string CurrentSystemPrompt,
    string? ModelName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt,
    IReadOnlyList<ThreadMessageRecord> Conversation,
    string FilePath)
{
    public int MessageCount => Conversation.Count;
}
