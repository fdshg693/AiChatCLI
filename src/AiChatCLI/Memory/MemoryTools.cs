using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Exposes the AI-callable durable memory tool backed by the app's local JSON memory store.
/// </summary>
public partial class MemoryTools
{
    /// <summary>
    /// Base tool name used in <c>Tools:Enabled</c>.
    /// </summary>
    public const string BaseToolName = "memory";

    private readonly MemoryStore _memoryStore;

    internal MemoryTools(MemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    /// <summary>
    /// Manages durable user memory stored in <c>memory.json</c>.
    /// Use <c>upsert</c> to create or update a fact, <c>get</c> to read one or more items,
    /// <c>list</c> to inspect stored keys only, and <c>delete</c> to remove an item that is no longer valid.
    /// After <c>list</c>, call <c>get</c> for any key whose value you need to inspect.
    /// Keep keys and values concise so the model can reliably reuse stable preferences, profile details, and project context.
    /// </summary>
    /// <param name="operation">The memory action to execute: <c>upsert</c>, <c>get</c>, <c>list</c>, or <c>delete</c>.</param>
    /// <param name="key">
    /// The memory key for <c>upsert</c>. For <c>get</c> and <c>delete</c>, this may be a comma-separated list of keys.
    /// Ignored for <c>list</c>. Memory keys themselves must not contain commas.
    /// </param>
    /// <param name="value">The value to persist when using <c>upsert</c>. Ignored by the other operations.</param>
    /// <param name="limit">For <c>list</c>, the maximum number of keys to return. Defaults to <c>5</c>. Use <c>-1</c> to return all keys.</param>
    /// <returns>A JSON string describing whether the operation succeeded and any returned memory data.</returns>
    [Function]
    public Task<string> MemoryStoreTool(string operation, string key = "", string value = "", int limit = 5)
    {
        var result = _memoryStore.ExecuteJson(operation, key, value, limit);
        return Task.FromResult(result);
    }
}
