using AutoGen.Core;

namespace AiChatCLI;

internal sealed record ThreadMessageRecord(
    ThreadMessageKind Kind,
    string? Role,
    string? Content,
    string? From,
    IReadOnlyList<ThreadToolCallRecord>? ToolCalls)
{
    public static ThreadMessageRecord CreateText(Role role, string content, string? from = null) =>
        new(ThreadMessageKind.Text, role.ToString(), content, from, null);

    public static ThreadMessageRecord CreateToolCallAggregate(
        IReadOnlyList<ThreadToolCallRecord> toolCalls,
        string? content = null,
        string? from = null) =>
        new(ThreadMessageKind.ToolCallAggregate, null, content, from, toolCalls);
}
