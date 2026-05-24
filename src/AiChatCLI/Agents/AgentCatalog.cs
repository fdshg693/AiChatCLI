using System.Text.Json;

namespace AiChatCLI;

internal sealed class AgentCatalog
{
    private const string DefaultAgentName = "default";
    private const string DefaultAgentPrompt = "You are a helpful assistant.";

    private readonly Dictionary<string, string> _agents;
    private readonly string _agentsPath;
    private readonly Func<string, string?> _resolveBuiltInPlaceholder;
    private readonly int _maxPlaceholderDepth;

    public AgentCatalog(
        string agentsPath,
        IReadOnlyDictionary<string, string>? builtInPlaceholders = null,
        int maxPlaceholderDepth = 10)
        : this(
            agentsPath,
            CreateBuiltInPlaceholderResolver(builtInPlaceholders),
            maxPlaceholderDepth)
    {
    }

    internal AgentCatalog(
        string agentsPath,
        Func<string, string?>? builtInPlaceholderResolver,
        int maxPlaceholderDepth = 10)
    {
        _agentsPath = agentsPath;
        _resolveBuiltInPlaceholder = builtInPlaceholderResolver ?? (_ => null);
        _maxPlaceholderDepth = maxPlaceholderDepth;

        var loaded = LoadAgents(
            agentsPath,
            _resolveBuiltInPlaceholder,
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
                _resolveBuiltInPlaceholder,
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

    private static Func<string, string?> CreateBuiltInPlaceholderResolver(
        IReadOnlyDictionary<string, string>? builtInPlaceholders)
    {
        if (builtInPlaceholders is null || builtInPlaceholders.Count == 0)
            return _ => null;

        return key => builtInPlaceholders.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static (Dictionary<string, string> Agents, string SourcePath) LoadAgents(
        string agentsPath,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth)
    {
        var sourcePath = agentsPath;
        Dictionary<string, string> agents;
        string? systemPromptPrefix;

        if (File.Exists(agentsPath))
        {
            var json = File.ReadAllText(agentsPath);
            var loaded = ParseAgentFile(json);
            agents = loaded.Agents;
            systemPromptPrefix = loaded.SystemPromptPrefix;
        }
        else
        {
            agents = new();
            systemPromptPrefix = null;
        }

        if (!agents.ContainsKey(DefaultAgentName))
            agents[DefaultAgentName] = DefaultAgentPrompt;

        var expandedAgents = ExpandAgentPlaceholders(agents, resolveBuiltInPlaceholder, maxPlaceholderDepth);
        if (string.IsNullOrWhiteSpace(systemPromptPrefix))
            return (expandedAgents, sourcePath);

        var expandedCommonPrompt = ExpandPrompt(systemPromptPrefix, agents, resolveBuiltInPlaceholder, maxPlaceholderDepth);
        return string.IsNullOrWhiteSpace(expandedCommonPrompt)
            ? (expandedAgents, sourcePath)
            : (PrependCommonPrompt(expandedAgents, expandedCommonPrompt), sourcePath);
    }

    private static (Dictionary<string, string> Agents, string? SystemPromptPrefix) ParseAgentFile(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("agents.json のルートは object である必要があります。");

        Dictionary<string, string>? agents = null;
        string? systemPromptPrefix = null;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "defaults":
                    systemPromptPrefix = ReadDefaults(property.Value);
                    break;
                case "agents":
                    agents = ReadAgents(property.Value);
                    break;
                default:
                    throw new JsonException($"agents.json の未対応キーです: {property.Name}");
            }
        }

        return (agents ?? new Dictionary<string, string>(StringComparer.Ordinal), systemPromptPrefix);
    }

    private static string? ReadDefaults(JsonElement defaultsElement)
    {
        if (defaultsElement.ValueKind == JsonValueKind.Null)
            return null;

        if (defaultsElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("agents.json の defaults は object である必要があります。");

        string? systemPromptPrefix = null;
        foreach (var property in defaultsElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "systemPromptPrefix":
                    systemPromptPrefix = property.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => property.Value.GetString(),
                        _ => throw new JsonException("agents.json の defaults.systemPromptPrefix は string である必要があります。")
                    };
                    break;
                default:
                    throw new JsonException($"agents.json の defaults に未対応キーがあります: {property.Name}");
            }
        }

        return systemPromptPrefix;
    }

    private static Dictionary<string, string> ReadAgents(JsonElement agentsElement)
    {
        if (agentsElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("agents.json の agents は object である必要があります。");

        var agents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in agentsElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new JsonException($"agents.json の agents.{property.Name} は string である必要があります。");

            agents[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return agents;
    }

    private static Dictionary<string, string> ExpandAgentPlaceholders(
        Dictionary<string, string> agents,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth)
    {
        var expanded = new Dictionary<string, string>(agents.Comparer);
        foreach (var (name, prompt) in agents)
            expanded[name] = ExpandPrompt(prompt, agents, resolveBuiltInPlaceholder, maxPlaceholderDepth);

        return expanded;
    }

    private static string ExpandPrompt(
        string prompt,
        IReadOnlyDictionary<string, string> agents,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth) =>
        PlaceholderExpander.Expand(
            prompt,
            key => ResolvePlaceholder(key, agents, resolveBuiltInPlaceholder),
            maxPlaceholderDepth);

    private static Dictionary<string, string> PrependCommonPrompt(
        Dictionary<string, string> agents,
        string commonPrompt)
    {
        var prefixed = new Dictionary<string, string>(agents.Comparer);
        foreach (var (agentName, prompt) in agents)
            prefixed[agentName] = string.IsNullOrEmpty(prompt)
                ? commonPrompt
                : $"{commonPrompt}\n\n{prompt}";

        return prefixed;
    }

    private static string? ResolvePlaceholder(
        string key,
        IReadOnlyDictionary<string, string> agents,
        Func<string, string?> resolveBuiltInPlaceholder)
    {
        var builtInValue = resolveBuiltInPlaceholder(key);
        if (builtInValue is not null)
            return builtInValue;

        return agents.TryGetValue(key, out var agentPrompt)
            ? agentPrompt
            : null;
    }

}
