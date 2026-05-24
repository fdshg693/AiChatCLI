namespace AiChatCLI;

internal interface IChatService
{
    Task<ChatTurnResult> SendAsync(string message);
    void SetAgent(string agentName, string systemPrompt, IReadOnlySet<string> enabledTools);
    void RestoreConversation(
        string agentName,
        string systemPrompt,
        IReadOnlySet<string> enabledTools,
        IReadOnlyList<ThreadMessageRecord> conversation);
}
