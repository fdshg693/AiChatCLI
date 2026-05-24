using AutoGen.Core;

namespace AiChatCLI.Tests;

public sealed class ThreadProjectorTests
{
    private readonly ThreadProjector _projector = new(new ConversationCodec());

    [Fact]
    public void Project_RehydratesConversationAndLatestAgentState()
    {
        const string threadId = "thread_1";
        var events = new[]
        {
            ThreadEvent.ThreadCreated(threadId, "gpt-4o-mini", "default", "You are a helpful assistant."),
            ThreadEvent.ModelRequest(threadId, "session_1", "default", "hello"),
            ThreadEvent.ToolCall(
                threadId,
                "session_1",
                "default",
                "assistant",
                "thinking",
                [
                    new ThreadToolCallRecord("memory_store", "{\"key\":\"language\"}", null, "call_1")
                ]),
            ThreadEvent.SubAgentInvoked(
                threadId,
                "session_1",
                "default",
                "subagent_thread_1",
                "call_sub_1"),
            ThreadEvent.ToolResult(
                threadId,
                "session_1",
                "default",
                "assistant",
                [
                    new ThreadToolCallRecord("memory_store", "{\"key\":\"language\"}", "{\"ok\":true}", "call_1")
                ]),
            ThreadEvent.AssistantMessage(
                threadId,
                "session_1",
                "default",
                ThreadMessageRecord.CreateText(Role.Assistant, "done", "assistant")),
            ThreadEvent.AgentChanged(
                threadId,
                "session_1",
                "translator",
                "You are a translator.",
                "agent_use")
        };

        var snapshot = _projector.Project("thread_1.jsonl", events);

        Assert.Equal("translator", snapshot.CurrentAgentName);
        Assert.Equal("You are a translator.", snapshot.CurrentSystemPrompt);
        Assert.Equal(3, snapshot.Conversation.Count);

        var userMessage = snapshot.Conversation[0];
        Assert.Equal(ThreadMessageKind.Text, userMessage.Kind);
        Assert.Equal(Role.User.ToString(), userMessage.Role);
        Assert.Equal("hello", userMessage.Content);

        var toolAggregate = snapshot.Conversation[1];
        Assert.Equal(ThreadMessageKind.ToolCallAggregate, toolAggregate.Kind);
        var toolCall = Assert.Single(toolAggregate.ToolCalls!);
        Assert.Equal("{\"ok\":true}", toolCall.Result);

        var assistantMessage = snapshot.Conversation[2];
        Assert.Equal(ThreadMessageKind.Text, assistantMessage.Kind);
        Assert.Equal(Role.Assistant.ToString(), assistantMessage.Role);
        Assert.Equal("done", assistantMessage.Content);
    }

    [Fact]
    public void Project_PreservesPendingToolCallAtEndOfLog()
    {
        const string threadId = "thread_2";
        var events = new[]
        {
            ThreadEvent.ThreadCreated(threadId, "gpt-4o-mini", "default", "You are a helpful assistant."),
            ThreadEvent.ToolCall(
                threadId,
                "session_1",
                "default",
                "assistant",
                "thinking",
                [
                    new ThreadToolCallRecord("memory_store", "{\"key\":\"language\"}", null, "call_1")
                ])
        };

        var snapshot = _projector.Project("thread_2.jsonl", events);

        var aggregate = Assert.Single(snapshot.Conversation);
        Assert.Equal(ThreadMessageKind.ToolCallAggregate, aggregate.Kind);
        var toolCall = Assert.Single(aggregate.ToolCalls!);
        Assert.Null(toolCall.Result);
        Assert.Equal("call_1", toolCall.ToolCallId);
    }
}
