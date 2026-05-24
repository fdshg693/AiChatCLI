namespace AiChatCLI;

public sealed class AgentSelection
{
    private readonly AgentCatalog _catalog;

    public AgentSelection(AgentCatalog catalog)
    {
        _catalog = catalog;
        EnsureCurrentSelection();
    }

    public string CurrentName { get; private set; } = "default";

    public string CurrentPrompt { get; private set; } = "You are a helpful assistant.";

    public bool TrySelect(string name)
    {
        if (!_catalog.TryGetAgentPrompt(name, out var prompt))
            return false;

        CurrentName = name;
        CurrentPrompt = prompt;
        return true;
    }

    public void SetCurrent(string name, string prompt)
    {
        CurrentName = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();
        CurrentPrompt = prompt ?? string.Empty;
    }

    public void EnsureCurrentSelection()
    {
        if (_catalog.TryGetAgentPrompt(CurrentName, out var prompt))
        {
            CurrentPrompt = prompt;
            return;
        }

        CurrentName = "default";
        CurrentPrompt = _catalog.TryGetAgentPrompt(CurrentName, out var defaultPrompt)
            ? defaultPrompt
            : "You are a helpful assistant.";
    }
}
