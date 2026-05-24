using AutoGen.Core;

namespace AiChatCLI;

internal enum AgentToolScope
{
    MainAndSubAgent,
    MainAgentOnly
}

internal enum AgentToolConsumer
{
    MainAgent,
    SubAgent
}

internal sealed record AgentToolDescriptor(
    string Name,
    FunctionContract Contract,
    Func<string, Task<string>> InvokeAsync,
    AgentToolScope Scope);
