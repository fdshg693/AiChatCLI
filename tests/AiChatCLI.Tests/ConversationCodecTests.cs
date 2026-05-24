using AutoGen.Core;

namespace AiChatCLI.Tests;

public sealed class ConversationCodecTests
{
    private readonly ConversationCodec _codec = new();

    [Fact]
    public void TextMessage_RoundTripsThroughCodec()
    {
        var original = ThreadMessageRecord.CreateText(Role.User, "hello", "user");

        var roundTrip = _codec.ToRecord(_codec.ToMessage(original));

        Assert.Equal(original.Kind, roundTrip.Kind);
        Assert.Equal(original.Role, roundTrip.Role);
        Assert.Equal(original.Content, roundTrip.Content);
        Assert.Equal(original.From, roundTrip.From);
        Assert.Null(roundTrip.ToolCalls);
    }

    [Fact]
    public void ToolCallAggregate_RoundTripsThroughCodec()
    {
        var original = ThreadMessageRecord.CreateToolCallAggregate(
            [
                new ThreadToolCallRecord(
                    "memory_store",
                    "{\"key\":\"language\"}",
                    "{\"ok\":true}",
                    "call_1")
            ],
            "thinking",
            "assistant");

        var roundTrip = _codec.ToRecord(_codec.ToMessage(original));

        Assert.Equal(original.Kind, roundTrip.Kind);
        Assert.Equal(original.Content, roundTrip.Content);
        Assert.Equal(original.From, roundTrip.From);

        var toolCall = Assert.Single(roundTrip.ToolCalls!);
        Assert.Equal("memory_store", toolCall.FunctionName);
        Assert.Equal("{\"key\":\"language\"}", toolCall.FunctionArguments);
        Assert.Equal("{\"ok\":true}", toolCall.Result);
        Assert.Equal("call_1", toolCall.ToolCallId);
    }

    [Fact]
    public void CreateToolCallAggregateRecord_MergesRequestAndResultLists()
    {
        var aggregate = _codec.CreateToolCallAggregateRecord(
            [
                new ThreadToolCallRecord(
                    "memory_store",
                    "{\"key\":\"language\"}",
                    null,
                    "call_1")
            ],
            [
                new ThreadToolCallRecord(
                    "memory_store",
                    "{\"key\":\"language\"}",
                    "{\"ok\":true}",
                    "call_1")
            ],
            "thinking",
            "assistant");

        var toolCall = Assert.Single(aggregate.ToolCalls!);
        Assert.Equal("{\"ok\":true}", toolCall.Result);
        Assert.Equal("call_1", toolCall.ToolCallId);
    }
}
