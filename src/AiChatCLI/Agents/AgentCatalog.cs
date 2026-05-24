using System.Text.Json;

namespace AiChatCLI;

internal sealed class AgentCatalog
{
    private const string DefaultAgentName = "default";
    private const string DefaultAgentPrompt = "You are a helpful assistant.";
    private static readonly string[] DefaultAgentTools =
    [
        MemoryTools.BaseToolName,
        SubAgentTools.FunctionName,
        CommandTools.BaseToolName,
        FileReadTools.BaseToolName
    ];

    private readonly Dictionary<string, AgentDefinition> _agents;
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

    public IReadOnlyDictionary<string, AgentDefinition> GetAgents() => _agents;

    public bool TryGetAgent(string name, out AgentDefinition definition) => _agents.TryGetValue(name, out definition!);

    public bool TryGetAgentPrompt(string name, out string prompt)
    {
        if (_agents.TryGetValue(name, out var definition))
        {
            prompt = definition.Prompt;
            return true;
        }

        prompt = string.Empty;
        return false;
    }

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

    private static (Dictionary<string, AgentDefinition> Agents, string SourcePath) LoadAgents(
        string agentsPath,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth)
    {
        var sourcePath = agentsPath;
        Dictionary<string, AgentDefinition> agents;
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
            agents[DefaultAgentName] = new AgentDefinition(DefaultAgentPrompt, DefaultAgentTools);

        var expandedAgents = ExpandAgentPrompts(agents, resolveBuiltInPlaceholder, maxPlaceholderDepth);
        if (string.IsNullOrWhiteSpace(systemPromptPrefix))
            return (expandedAgents, sourcePath);

        var expandedCommonPrompt = ExpandCommonPrompt(
            systemPromptPrefix,
            agents,
            resolveBuiltInPlaceholder,
            maxPlaceholderDepth);
        return string.IsNullOrWhiteSpace(expandedCommonPrompt)
            ? (expandedAgents, sourcePath)
            : (PrependCommonPrompt(expandedAgents, expandedCommonPrompt), sourcePath);
    }

    private static (Dictionary<string, AgentDefinition> Agents, string? SystemPromptPrefix) ParseAgentFile(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("agents.json のルートは object である必要があります。");

        Dictionary<string, AgentDefinition>? agents = null;
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

        return (agents ?? new Dictionary<string, AgentDefinition>(StringComparer.Ordinal), systemPromptPrefix);
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

    private static Dictionary<string, AgentDefinition> ReadAgents(JsonElement agentsElement)
    {
        if (agentsElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("agents.json の agents は object である必要があります。");

        var agents = new Dictionary<string, AgentDefinition>(StringComparer.Ordinal);
        foreach (var property in agentsElement.EnumerateObject())
            agents[property.Name] = ReadAgentDefinition(property.Name, property.Value);

        return agents;
    }

    private static AgentDefinition ReadAgentDefinition(string agentName, JsonElement agentElement)
    {
        if (agentElement.ValueKind != JsonValueKind.Object)
            throw new JsonException($"agents.json の agents.{agentName} は object である必要があります。");

        string? prompt = null;
        List<string>? tools = null;

        foreach (var property in agentElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "prompt":
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new JsonException($"agents.json の agents.{agentName}.prompt は string である必要があります。");

                    prompt = property.Value.GetString() ?? string.Empty;
                    break;
                case "tools":
                    tools = ReadAgentTools(agentName, property.Value);
                    break;
                default:
                    throw new JsonException($"agents.json の agents.{agentName} に未対応キーがあります: {property.Name}");
            }
        }

        if (prompt is null)
            throw new JsonException($"agents.json の agents.{agentName}.prompt は必須です。");

        return new AgentDefinition(prompt, tools ?? []);
    }

    private static List<string> ReadAgentTools(string agentName, JsonElement toolsElement)
    {
        if (toolsElement.ValueKind != JsonValueKind.Array)
            throw new JsonException($"agents.json の agents.{agentName}.tools は array である必要があります。");

        var tools = new List<string>();
        foreach (var item in toolsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new JsonException($"agents.json の agents.{agentName}.tools は string の配列である必要があります。");

            var toolName = item.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(toolName))
                tools.Add(toolName);
        }

        return tools;
    }

    private static Dictionary<string, AgentDefinition> ExpandAgentPrompts(
        Dictionary<string, AgentDefinition> agents,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth)
    {
        var prompts = agents.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Prompt,
            agents.Comparer);
        var expanded = new Dictionary<string, AgentDefinition>(agents.Comparer);
        foreach (var (name, definition) in agents)
        {
            expanded[name] = new AgentDefinition(
                ExpandPrompt(definition.Prompt, prompts, resolveBuiltInPlaceholder, maxPlaceholderDepth),
                definition.EnabledTools);
        }

        return expanded;
    }

    private static string ExpandCommonPrompt(
        string commonPrompt,
        IReadOnlyDictionary<string, AgentDefinition> agents,
        Func<string, string?> resolveBuiltInPlaceholder,
        int maxPlaceholderDepth)
    {
        var prompts = agents.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Prompt,
            StringComparer.Ordinal);

        return ExpandPrompt(commonPrompt, prompts, resolveBuiltInPlaceholder, maxPlaceholderDepth);
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

    private static Dictionary<string, AgentDefinition> PrependCommonPrompt(
        Dictionary<string, AgentDefinition> agents,
        string commonPrompt)
    {
        var prefixed = new Dictionary<string, AgentDefinition>(agents.Comparer);
        foreach (var (agentName, definition) in agents)
        {
            var prompt = string.IsNullOrEmpty(definition.Prompt)
                ? commonPrompt
                : $"{commonPrompt}\n\n{definition.Prompt}";
            prefixed[agentName] = new AgentDefinition(prompt, definition.EnabledTools);
        }

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
