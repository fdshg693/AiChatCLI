namespace AiChatCLI;

internal sealed record ToolExecutionRecord(
    string FunctionName,
    string FunctionArguments,
    string? Result,
    string? ToolCallId);
