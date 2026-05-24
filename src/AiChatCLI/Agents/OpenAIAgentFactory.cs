using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using OpenAI;

namespace AiChatCLI;

internal sealed class OpenAIAgentFactory
{
    private readonly OpenAIClient _client;
    private readonly string _model;
    private readonly AgentToolCatalog _toolCatalog;

    public OpenAIAgentFactory(string apiKey, string model, AgentToolCatalog toolCatalog)
    {
        _client = new OpenAIClient(apiKey);
        _model = model;
        _toolCatalog = toolCatalog;
    }

    public IAgent CreateMainAgent(string agentName, string systemPrompt) =>
        Create(agentName, systemPrompt, AgentToolConsumer.MainAgent);

    public IAgent CreateSubAgent(string agentName, string systemPrompt) =>
        Create(agentName, systemPrompt, AgentToolConsumer.SubAgent);

    private IAgent Create(string agentName, string systemPrompt, AgentToolConsumer toolConsumer)
    {
        var effectiveAgentName = string.IsNullOrWhiteSpace(agentName)
            ? "default"
            : agentName.Trim();

        var (functions, functionMap) = _toolCatalog.GetBindings(toolConsumer);
        IAgent agent = new OpenAIChatAgent(
            chatClient: _client.GetChatClient(_model),
            name: effectiveAgentName,
            systemMessage: systemPrompt)
            .RegisterMessageConnector();

        if (functions.Count == 0)
            return agent;

        var functionCallMiddleware = new FunctionCallMiddleware(
            functions: functions,
            functionMap: functionMap);

        return agent.RegisterMiddleware(functionCallMiddleware);
    }
}
