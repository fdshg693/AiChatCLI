using AutoGen.Core;

namespace AiChatCLI;

public interface IAgentTurnExecutor
{
    Task<AgentTurnExecution> ExecuteAsync(IAgent agent, List<IMessage> turnHistory);
}

public sealed class AgentTurnExecutor : IAgentTurnExecutor
{
    private readonly ConversationCodec _conversationCodec;

    public AgentTurnExecutor(ConversationCodec conversationCodec)
    {
        _conversationCodec = conversationCodec;
    }

    public async Task<AgentTurnExecution> ExecuteAsync(IAgent agent, List<IMessage> turnHistory)
    {
        var toolExecutions = new List<ToolExecutionRecord>();
        var responseMessages = new List<ThreadMessageRecord>();

        IMessage reply = await agent.SendAsync(chatHistory: turnHistory);
        turnHistory.Add(reply);
        responseMessages.Add(_conversationCodec.ToRecord(reply));

        while (reply is ToolCallAggregateMessage toolCallAggregate)
        {
            RecordToolExecutions(toolCallAggregate, toolExecutions);

            reply = await agent.SendAsync(chatHistory: turnHistory);
            turnHistory.Add(reply);
            responseMessages.Add(_conversationCodec.ToRecord(reply));
        }

        return new AgentTurnExecution(
            new ChatTurnResult(
                reply.GetContent() ?? string.Empty,
                toolExecutions,
                responseMessages),
            turnHistory);
    }

    private static void RecordToolExecutions(
        ToolCallAggregateMessage toolCallAggregate,
        List<ToolExecutionRecord> toolExecutions)
    {
        toolExecutions.AddRange(toolCallAggregate.Message2.ToolCalls.Select(toolCall =>
            new ToolExecutionRecord(
                toolCall.FunctionName,
                toolCall.FunctionArguments,
                toolCall.Result,
                toolCall.ToolCallId)));
    }
}

public sealed record AgentTurnExecution(
    ChatTurnResult TurnResult,
    IReadOnlyList<IMessage> TurnHistory);
