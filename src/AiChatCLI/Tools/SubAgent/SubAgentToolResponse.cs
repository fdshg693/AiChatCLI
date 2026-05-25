namespace AiChatCLI;

internal sealed record SubAgentToolResponse(
    bool Ok,
    string? SubAgentThreadId,
    string? Result,
    string? Error);
