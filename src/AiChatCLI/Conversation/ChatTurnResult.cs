namespace AiChatCLI;

public sealed record ChatTurnResult(
    string FinalReply,
    IReadOnlyList<ToolExecutionRecord> ToolExecutions,
    IReadOnlyList<ThreadMessageRecord> ResponseMessages);
