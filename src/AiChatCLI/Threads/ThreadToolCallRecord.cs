namespace AiChatCLI;

internal sealed record ThreadToolCallRecord(
    string FunctionName,
    string FunctionArguments,
    string? Result,
    string? ToolCallId);
