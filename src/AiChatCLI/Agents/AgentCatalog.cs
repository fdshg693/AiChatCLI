using System.Text.Json;

namespace AiChatCLI;

internal sealed class AgentCatalog
{
    private readonly Dictionary<string, string> _agents;
    private readonly string _agentsPath;
    private readonly string? _legacySystemPromptsPath;
    private readonly IReadOnlyDictionary<string, string> _builtInPlaceholders;
    private readonly int _maxPlaceholderDepth;

    public AgentCatalog(
        string agentsPath,
        string? legacySystemPromptsPath = null,
        IReadOnlyDictionary<string, string>? builtInPlaceholders = null,
        int maxPlaceholderDepth = 10)
    {
        _agentsPath = agentsPath;
        _legacySystemPromptsPath = legacySystemPromptsPath;
        _builtInPlaceholders = builtInPlaceholders ?? new Dictionary<string, string>();
        _maxPlaceholderDepth = maxPlaceholderDepth;

        var loaded = LoadAgents(
            agentsPath,
            legacySystemPromptsPath,
            _builtInPlaceholders,
            _maxPlaceholderDepth);
        _agents = loaded.Agents;
        SourcePath = loaded.SourcePath;
    }

    public string SourcePath { get; private set; }

    public IReadOnlyDictionary<string, string> GetAgents() => _agents;

    public bool TryGetAgentPrompt(string name, out string prompt) => _agents.TryGetValue(name, out prompt!);

    public bool ContainsAgent(string name) => _agents.ContainsKey(name);

    public bool ReloadAgentsFromDisk()
    {
        try
        {
            var loaded = LoadAgents(
                _agentsPath,
                _legacySystemPromptsPath,
                _builtInPlaceholders,
                _maxPlaceholderDepth);
            _agents.Clear();
            foreach (var kv in loaded.Agents)
                _agents[kv.Key] = kv.Value;

            SourcePath = loaded.SourcePath;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (Dictionary<string, string> Agents, string SourcePath) LoadAgents(
        string agentsPath,
        string? legacySystemPromptsPath,
        IReadOnlyDictionary<string, string> builtInPlaceholders,
        int maxPlaceholderDepth)
    {
        var sourcePath = ResolveSourcePath(agentsPath, legacySystemPromptsPath);
        Dictionary<string, string> agents;

        if (File.Exists(sourcePath))
        {
            var json = File.ReadAllText(sourcePath);
            agents = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        else
        {
            agents = new();
        }

        if (!agents.ContainsKey("default"))
            agents["default"] = "You are a helpful assistant.";

        return (ExpandAgentPlaceholders(agents, builtInPlaceholders, maxPlaceholderDepth), sourcePath);
    }

    private static Dictionary<string, string> ExpandAgentPlaceholders(
        Dictionary<string, string> agents,
        IReadOnlyDictionary<string, string> builtInPlaceholders,
        int maxPlaceholderDepth)
    {
        var expanded = new Dictionary<string, string>(agents.Comparer);
        foreach (var (name, prompt) in agents)
            expanded[name] = PlaceholderExpander.Expand(prompt, ResolvePlaceholder, maxPlaceholderDepth);

        return expanded;

        string? ResolvePlaceholder(string key)
        {
            if (builtInPlaceholders.TryGetValue(key, out var builtInValue))
                return builtInValue;

            return agents.TryGetValue(key, out var agentPrompt)
                ? agentPrompt
                : null;
        }
    }

    private static string ResolveSourcePath(string agentsPath, string? legacySystemPromptsPath)
    {
        if (File.Exists(agentsPath))
            return agentsPath;

        if (!string.IsNullOrWhiteSpace(legacySystemPromptsPath) && File.Exists(legacySystemPromptsPath))
            return legacySystemPromptsPath;

        return agentsPath;
    }
}
