namespace AiChatCLI;

internal sealed record ThreadSummary(
    string ThreadId,
    string CurrentAgentName,
    string CurrentSystemPrompt,
    string? ModelName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt,
    int MessageCount,
    string FilePath);
