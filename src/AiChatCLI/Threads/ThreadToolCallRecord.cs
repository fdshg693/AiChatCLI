namespace AiChatCLI;

public sealed record ThreadToolCallRecord(
    string FunctionName,
    string FunctionArguments,
    string? Result,
    string? ToolCallId);
