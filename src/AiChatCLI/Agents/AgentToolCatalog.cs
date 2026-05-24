using AutoGen.Core;

namespace AiChatCLI;

internal sealed class AgentToolCatalog
{
    private readonly HashSet<string> _enabledBaseTools;
    private readonly List<AgentToolDescriptor> _tools = [];

    public AgentToolCatalog(IReadOnlySet<string> enabledBaseTools)
    {
        _enabledBaseTools = new HashSet<string>(enabledBaseTools, StringComparer.OrdinalIgnoreCase);
    }

    public void RegisterMemoryTool(MemoryTools memoryTools)
    {
        Register(new AgentToolDescriptor(
            MemoryTools.BaseToolName,
            memoryTools.MemoryStoreToolFunctionContract,
            memoryTools.MemoryStoreToolWrapper,
            AgentToolScope.MainAndSubAgent));
    }

    public void RegisterSubAgentTool(SubAgentTools subAgentTools)
    {
        Register(new AgentToolDescriptor(
            SubAgentTools.FunctionName,
            subAgentTools.sub_agentFunctionContract,
            subAgentTools.sub_agentWrapper,
            AgentToolScope.MainAgentOnly));
    }

    public void RegisterSearchTool(TavilySearchTools searchTools)
    {
        Register(new AgentToolDescriptor(
            TavilySearchTools.BaseToolName,
            searchTools.searchFunctionContract,
            searchTools.searchWrapper,
            AgentToolScope.MainAndSubAgent));
    }

    /// <summary>
    /// Registers the approval-gated local command execution tool.
    /// </summary>
    public void RegisterCommandTool(CommandTools commandTools)
    {
        Register(new AgentToolDescriptor(
            CommandTools.BaseToolName,
            commandTools.commandFunctionContract,
            commandTools.commandWrapper,
            AgentToolScope.MainAndSubAgent));
    }

    public IReadOnlyList<string> GetEnabledToolNames(AgentToolConsumer consumer) =>
        GetEnabledTools(consumer)
            .Select(tool => tool.Name)
            .ToArray();

    public (IReadOnlyList<FunctionContract> Functions, Dictionary<string, Func<string, Task<string>>> FunctionMap)
        GetBindings(AgentToolConsumer consumer)
    {
        var functions = new List<FunctionContract>();
        var functionMap = new Dictionary<string, Func<string, Task<string>>>(StringComparer.Ordinal);

        foreach (var tool in GetEnabledTools(consumer))
        {
            functions.Add(tool.Contract);
            functionMap[tool.Contract.Name] = tool.InvokeAsync;
        }

        return (functions, functionMap);
    }

    private IReadOnlyList<AgentToolDescriptor> GetEnabledTools(AgentToolConsumer consumer) =>
        _tools
            .Where(tool => IsBaseToolEnabled(tool.Name) && IsAvailableForConsumer(tool, consumer))
            .ToArray();

    private static bool IsAvailableForConsumer(AgentToolDescriptor tool, AgentToolConsumer consumer) =>
        consumer == AgentToolConsumer.MainAgent || tool.Scope == AgentToolScope.MainAndSubAgent;

    private bool IsBaseToolEnabled(string toolName) => _enabledBaseTools.Contains(toolName);

    private void Register(AgentToolDescriptor descriptor)
    {
        _tools.RemoveAll(tool => string.Equals(tool.Name, descriptor.Name, StringComparison.Ordinal));
        _tools.Add(descriptor);
    }
}
