namespace AiChatCLI;

internal interface IChatService
{
    Task<ChatTurnResult> SendAsync(string message);
    void SetAgent(string agentName, string systemPrompt);
    void RestoreConversation(string agentName, string systemPrompt, IReadOnlyList<ThreadMessageRecord> conversation);
}
