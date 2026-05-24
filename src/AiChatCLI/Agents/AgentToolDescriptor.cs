using AutoGen.Core;

namespace AiChatCLI;

public enum AgentToolScope
{
    MainAndSubAgent,
    MainAgentOnly
}

public enum AgentToolConsumer
{
    MainAgent,
    SubAgent
}

public sealed record AgentToolDescriptor(
    string Name,
    FunctionContract Contract,
    Func<string, Task<string>> InvokeAsync,
    AgentToolScope Scope);
