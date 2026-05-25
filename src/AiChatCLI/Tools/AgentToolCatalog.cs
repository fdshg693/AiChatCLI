using AutoGen.Core;

namespace AiChatCLI;

internal sealed class AgentToolCatalog
{
    private readonly List<AgentToolDescriptor> _tools = [];
    private static readonly HashSet<string> KnownToolNamesSet =
        new(
            [
                MemoryTools.BaseToolName,
                SubAgentTools.FunctionName,
                TavilySearchTools.BaseToolName,
                FileReadTools.BaseToolName,
                CommandTools.BaseToolName,
                SkillTools.BaseToolName
            ],
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> KnownToolNames => KnownToolNamesSet;

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

    public void RegisterFileReadTool(FileReadTools fileReadTools)
    {
        Register(new AgentToolDescriptor(
            FileReadTools.BaseToolName,
            fileReadTools.read_fileFunctionContract,
            fileReadTools.read_fileWrapper,
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

    public void RegisterSkillTool(SkillTools skillTools)
    {
        Register(new AgentToolDescriptor(
            SkillTools.BaseToolName,
            skillTools.skillFunctionContract,
            skillTools.skillWrapper,
            AgentToolScope.MainAndSubAgent));
    }

    public IReadOnlyList<string> GetEnabledToolNames(
        IReadOnlySet<string> enabledToolNames,
        AgentToolConsumer consumer) =>
        GetEnabledTools(enabledToolNames, consumer)
            .Select(tool => tool.Name)
            .ToArray();

    public (IReadOnlyList<FunctionContract> Functions, Dictionary<string, Func<string, Task<string>>> FunctionMap)
        GetBindings(IReadOnlySet<string> enabledToolNames, AgentToolConsumer consumer)
    {
        var functions = new List<FunctionContract>();
        var functionMap = new Dictionary<string, Func<string, Task<string>>>(StringComparer.Ordinal);

        foreach (var tool in GetEnabledTools(enabledToolNames, consumer))
        {
            functions.Add(tool.Contract);
            functionMap[tool.Contract.Name] = tool.InvokeAsync;
        }

        return (functions, functionMap);
    }

    public static IReadOnlyList<string> FindUnknownToolNames(IEnumerable<string> toolNames) =>
        toolNames
            .Where(toolName => !KnownToolNamesSet.Contains(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IReadOnlyList<AgentToolDescriptor> GetEnabledTools(
        IReadOnlySet<string> enabledToolNames,
        AgentToolConsumer consumer) =>
        _tools
            .Where(tool => enabledToolNames.Contains(tool.Name) && IsAvailableForConsumer(tool, consumer))
            .ToArray();

    private static bool IsAvailableForConsumer(AgentToolDescriptor tool, AgentToolConsumer consumer) =>
        consumer == AgentToolConsumer.MainAgent || tool.Scope == AgentToolScope.MainAndSubAgent;

    private void Register(AgentToolDescriptor descriptor)
    {
        _tools.RemoveAll(tool => string.Equals(tool.Name, descriptor.Name, StringComparison.Ordinal));
        _tools.Add(descriptor);
    }
}
