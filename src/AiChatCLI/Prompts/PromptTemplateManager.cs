using System.Text.Json;

namespace AiChatCLI;

internal class PromptTemplateManager
{
    private readonly Dictionary<string, string> _templates;
    private readonly string _templatesPath;
    private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

    public PromptTemplateManager(string templatesPath)
    {
        _templatesPath = templatesPath;
        _templates = LoadTemplates(templatesPath);
    }

    public IReadOnlyDictionary<string, string> GetTemplates() => _templates;

    public bool TryGetTemplate(string key, out string value) => _templates.TryGetValue(key, out value!);

    public bool ContainsTemplate(string key) => _templates.ContainsKey(key);

    public bool TrySetTemplate(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        key = key.Trim();
        var hadKey = _templates.TryGetValue(key, out var previous);
        _templates[key] = value;

        try
        {
            Save();
            return true;
        }
        catch (IOException)
        {
            if (hadKey)
                _templates[key] = previous!;
            else
                _templates.Remove(key);

            return false;
        }
    }

    public bool TryRemoveTemplate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        key = key.Trim();
        if (!_templates.TryGetValue(key, out var previous))
            return false;

        _templates.Remove(key);

        try
        {
            Save();
            return true;
        }
        catch (IOException)
        {
            _templates[key] = previous;
            return false;
        }
    }

    public bool ReloadTemplatesFromDisk()
    {
        try
        {
            var loaded = LoadTemplates(_templatesPath);
            _templates.Clear();
            foreach (var kv in loaded)
                _templates[kv.Key] = kv.Value;

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

    private void Save()
    {
        var json = JsonSerializer.Serialize(_templates, s_writeOptions);
        File.WriteAllText(_templatesPath, json);
    }

    private static Dictionary<string, string> LoadTemplates(string templatesPath)
    {
        if (!File.Exists(templatesPath))
            return new Dictionary<string, string>();

        var json = File.ReadAllText(templatesPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>();
    }
}
