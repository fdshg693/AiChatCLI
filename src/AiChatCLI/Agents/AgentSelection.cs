namespace AiChatCLI;

internal sealed class AgentSelection
{
    private readonly AgentCatalog _catalog;
    private static readonly IReadOnlySet<string> EmptyTools =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public AgentSelection(AgentCatalog catalog)
    {
        _catalog = catalog;
        EnsureCurrentSelection();
    }

    public string CurrentName { get; private set; } = "default";

    public string CurrentPrompt { get; private set; } = "You are a helpful assistant.";

    public IReadOnlySet<string> CurrentTools { get; private set; } = EmptyTools;

    public bool TrySelect(string name)
    {
        if (!_catalog.TryGetAgent(name, out var definition))
            return false;

        CurrentName = name;
        CurrentPrompt = definition.Prompt;
        CurrentTools = definition.EnabledTools;
        return true;
    }

    public void SetCurrent(string name, string prompt, IReadOnlySet<string>? enabledTools = null)
    {
        CurrentName = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();
        CurrentPrompt = prompt ?? string.Empty;
        CurrentTools = enabledTools ?? ResolveTools(CurrentName);
    }

    public void EnsureCurrentSelection()
    {
        if (_catalog.TryGetAgent(CurrentName, out var definition))
        {
            CurrentPrompt = definition.Prompt;
            CurrentTools = definition.EnabledTools;
            return;
        }

        CurrentName = "default";
        if (_catalog.TryGetAgent(CurrentName, out var defaultDefinition))
        {
            CurrentPrompt = defaultDefinition.Prompt;
            CurrentTools = defaultDefinition.EnabledTools;
            return;
        }

        CurrentPrompt = "You are a helpful assistant.";
        CurrentTools = EmptyTools;
    }

    private IReadOnlySet<string> ResolveTools(string agentName)
    {
        return _catalog.TryGetAgent(agentName, out var definition)
            ? definition.EnabledTools
            : EmptyTools;
    }
}
