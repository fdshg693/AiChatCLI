using System.Text.Json;

namespace AiChatCLI;

public sealed class AgentCatalog
{
    private readonly Dictionary<string, string> _agents;
    private readonly string _agentsPath;
    private readonly string? _legacySystemPromptsPath;

    public AgentCatalog(string agentsPath, string? legacySystemPromptsPath = null)
    {
        _agentsPath = agentsPath;
        _legacySystemPromptsPath = legacySystemPromptsPath;

        var loaded = LoadAgents(agentsPath, legacySystemPromptsPath);
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
            var loaded = LoadAgents(_agentsPath, _legacySystemPromptsPath);
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
        string? legacySystemPromptsPath)
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

        return (agents, sourcePath);
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
