namespace AiChatCLI;

internal sealed record ChatTurnResult(
    string FinalReply,
    IReadOnlyList<ToolExecutionRecord> ToolExecutions,
    IReadOnlyList<ThreadMessageRecord> ResponseMessages);
