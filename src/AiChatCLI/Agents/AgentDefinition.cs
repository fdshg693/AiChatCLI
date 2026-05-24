namespace AiChatCLI;

internal sealed class AgentDefinition
{
    public AgentDefinition(string prompt, IEnumerable<string> enabledTools)
    {
        Prompt = prompt ?? string.Empty;
        EnabledTools = new HashSet<string>(
            enabledTools ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    public string Prompt { get; }

    public IReadOnlySet<string> EnabledTools { get; }
}
