using AutoGen.Core;

namespace AiChatCLI;

public sealed class ConversationCodec
{
    public ThreadMessageRecord ToRecord(IMessage message) =>
        message switch
        {
            TextMessage textMessage => ThreadMessageRecord.CreateText(
                textMessage.Role,
                textMessage.Content,
                textMessage.From),
            ToolCallAggregateMessage toolCallAggregate => CreateToolCallAggregateRecord(
                toolCallAggregate.Message1.ToolCalls.Select(ToThreadToolCallRecord).ToArray(),
                toolCallAggregate.Message2.ToolCalls.Select(ToThreadToolCallRecord).ToArray(),
                toolCallAggregate.Message1.Content,
                toolCallAggregate.From ?? toolCallAggregate.Message1.From ?? toolCallAggregate.Message2.From),
            _ => ThreadMessageRecord.CreateText(
                Role.Assistant,
                message.GetContent() ?? string.Empty)
        };

    public IMessage ToMessage(ThreadMessageRecord message) =>
        message.Kind switch
        {
            ThreadMessageKind.Text => new TextMessage(
                ParseRole(message.Role),
                message.Content ?? string.Empty,
                message.From),
            ThreadMessageKind.ToolCallAggregate => CreateToolCallAggregateMessage(message),
            _ => throw new InvalidOperationException($"未対応のメッセージ種別です: {message.Kind}")
        };

    public ThreadMessageRecord CreateToolCallAggregateRecord(
        IReadOnlyList<ThreadToolCallRecord>? toolCallItems,
        IReadOnlyList<ThreadToolCallRecord>? toolResultItems,
        string? content = null,
        string? from = null) =>
        ThreadMessageRecord.CreateToolCallAggregate(
            MergeToolCalls(toolCallItems, toolResultItems),
            content,
            from);

    public void SplitToolCallAggregate(
        ThreadMessageRecord message,
        out IReadOnlyList<ThreadToolCallRecord> toolCalls,
        out IReadOnlyList<ThreadToolCallRecord> toolResults)
    {
        if (message.Kind != ThreadMessageKind.ToolCallAggregate)
            throw new InvalidOperationException($"tool aggregate 以外は分解できません: {message.Kind}");

        var sourceToolCalls = message.ToolCalls?.ToArray() ?? [];
        toolCalls = sourceToolCalls
            .Select(toolCall => new ThreadToolCallRecord(
                toolCall.FunctionName,
                toolCall.FunctionArguments,
                null,
                toolCall.ToolCallId))
            .ToArray();
        toolResults = sourceToolCalls
            .Select(toolCall => new ThreadToolCallRecord(
                toolCall.FunctionName,
                toolCall.FunctionArguments,
                toolCall.Result,
                toolCall.ToolCallId))
            .ToArray();
    }

    public IReadOnlyList<ThreadToolCallRecord> MergeToolCalls(
        IReadOnlyList<ThreadToolCallRecord>? toolCallItems,
        IReadOnlyList<ThreadToolCallRecord>? toolResultItems)
    {
        if ((toolCallItems?.Count ?? 0) == 0 && (toolResultItems?.Count ?? 0) == 0)
            return [];

        if ((toolResultItems?.Count ?? 0) == 0)
            return toolCallItems!.ToArray();

        if ((toolCallItems?.Count ?? 0) == 0)
            return toolResultItems!.ToArray();

        var count = Math.Max(toolCallItems!.Count, toolResultItems!.Count);
        var merged = new List<ThreadToolCallRecord>(count);

        for (var index = 0; index < count; index++)
        {
            var toolCall = index < toolCallItems.Count ? toolCallItems[index] : toolResultItems[index];
            var toolResult = index < toolResultItems.Count ? toolResultItems[index] : toolCall;

            merged.Add(new ThreadToolCallRecord(
                toolResult.FunctionName,
                toolResult.FunctionArguments,
                toolResult.Result ?? toolCall.Result,
                toolResult.ToolCallId ?? toolCall.ToolCallId));
        }

        return merged;
    }

    private static ToolCallAggregateMessage CreateToolCallAggregateMessage(ThreadMessageRecord message)
    {
        var toolCalls = message.ToolCalls ?? [];
        var requestCalls = toolCalls
            .Select(CreateRequestToolCall)
            .ToArray();
        var resultCalls = toolCalls
            .Select(CreateResultToolCall)
            .ToArray();

        var toolCallMessage = new ToolCallMessage(requestCalls, message.From)
        {
            Content = message.Content
        };
        var toolResultMessage = new ToolCallResultMessage(resultCalls, message.From);
        return new ToolCallAggregateMessage(toolCallMessage, toolResultMessage, message.From);
    }

    private static ToolCall CreateRequestToolCall(ThreadToolCallRecord toolCallRecord) =>
        new(toolCallRecord.FunctionName, toolCallRecord.FunctionArguments)
        {
            ToolCallId = toolCallRecord.ToolCallId
        };

    private static ToolCall CreateResultToolCall(ThreadToolCallRecord toolCallRecord) =>
        new(toolCallRecord.FunctionName, toolCallRecord.FunctionArguments)
        {
            ToolCallId = toolCallRecord.ToolCallId,
            Result = toolCallRecord.Result
        };

    private static ThreadToolCallRecord ToThreadToolCallRecord(ToolCall toolCall) =>
        new(
            toolCall.FunctionName,
            toolCall.FunctionArguments,
            toolCall.Result,
            toolCall.ToolCallId);

    private static Role ParseRole(string? roleName) =>
        string.Equals(roleName, Role.User.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Role.User
            : Role.Assistant;
}
