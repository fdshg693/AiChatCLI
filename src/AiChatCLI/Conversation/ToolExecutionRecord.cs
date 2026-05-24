namespace AiChatCLI;

public sealed record ToolExecutionRecord(
    string FunctionName,
    string FunctionArguments,
    string? Result,
    string? ToolCallId);
