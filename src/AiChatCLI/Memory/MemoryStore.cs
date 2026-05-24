using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiChatCLI;

/// <summary>
/// Provides durable key-value storage for the memory tool using a single JSON file.
/// </summary>
internal sealed class MemoryStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a memory store that persists entries to the specified JSON file.
    /// </summary>
    /// <param name="filePath">The path of the backing memory file.</param>
    public MemoryStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the path of the JSON file that stores durable memory entries.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Gets the number of memory entries currently stored in the backing file.
    /// </summary>
    public int EntryCount
    {
        get
        {
            lock (_lock)
            {
                return LoadUnsafe().Items.Count;
            }
        }
    }

    /// <summary>
    /// Executes a memory operation and serializes the typed result as JSON for tool responses.
    /// </summary>
    /// <param name="operation">The requested operation name: upsert, get, list, or delete.</param>
    /// <param name="key">
    /// The memory key used by key-based operations.
    /// For <c>get</c> and <c>delete</c>, multiple keys may be provided as a comma-separated list.
    /// </param>
    /// <param name="value">The value to persist when the operation is <c>upsert</c>.</param>
    /// <param name="limit">The maximum number of keys to return for <c>list</c>. Use <c>-1</c> to return all keys.</param>
    /// <returns>A JSON payload describing the outcome of the memory operation.</returns>
    public string ExecuteJson(string operation, string? key, string? value, int limit = 5)
    {
        return SerializeResult(Execute(operation, key, value, limit));
    }

    /// <summary>
    /// Executes a typed memory operation against the durable memory store.
    /// </summary>
    /// <param name="operation">The requested operation name: upsert, get, list, or delete.</param>
    /// <param name="key">
    /// The memory key used by key-based operations.
    /// For <c>get</c> and <c>delete</c>, multiple keys may be provided as a comma-separated list.
    /// </param>
    /// <param name="value">The value to persist when the operation is <c>upsert</c>.</param>
    /// <param name="limit">The maximum number of keys to return for <c>list</c>. Use <c>-1</c> to return all keys.</param>
    /// <returns>A typed response containing the operation result and any returned memory data.</returns>
    public MemoryToolResponse Execute(string operation, string? key, string? value, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return MemoryToolResponse.Failure(error: "operation is required.");

        if (!TryParseOperation(operation, out var normalizedOperation))
        {
            return MemoryToolResponse.Failure(
                error: "operation must be one of: upsert, get, list, delete.");
        }

        return normalizedOperation switch
        {
            MemoryOperation.Upsert => Upsert(key, value),
            MemoryOperation.Get => Get(key),
            MemoryOperation.List => List(limit),
            MemoryOperation.Delete => Delete(key),
            _ => MemoryToolResponse.Failure(
                error: "operation must be one of: upsert, get, list, delete.")
        };
    }

    private MemoryToolResponse Upsert(string? key, string? value)
    {
        if (!TryNormalizeSingleKey(key, out var normalizedKey, out var error))
            return MemoryToolResponse.Failure(
                operation: "upsert",
                error: error);

        if (string.IsNullOrWhiteSpace(value))
            return MemoryToolResponse.Failure(
                operation: "upsert",
                key: normalizedKey,
                error: "value is required for upsert.");

        lock (_lock)
        {
            var model = LoadUnsafe();
            var existed = model.Items.ContainsKey(normalizedKey);
            model.Items[normalizedKey] = value.Trim();
            SaveUnsafe(model);

            return MemoryToolResponse.Success(
                operation: "upsert",
                key: normalizedKey,
                value: model.Items[normalizedKey],
                status: existed ? "updated" : "created");
        }
    }

    private MemoryToolResponse Get(string? key)
    {
        if (!TryParseKeyList(key, out var normalizedKeys, out var error))
            return MemoryToolResponse.Failure(
                operation: "get",
                error: error);

        lock (_lock)
        {
            var model = LoadUnsafe();
            var items = normalizedKeys
                .Select(currentKey =>
                {
                    var found = model.Items.TryGetValue(currentKey, out var storedValue);
                    return new MemoryValueItem(
                        Key: currentKey,
                        Value: found ? storedValue : null,
                        Found: found);
                })
                .ToArray();

            if (items.Length == 1)
            {
                var item = items[0];
                return item.Found
                    ? MemoryToolResponse.Success(
                        operation: "get",
                        key: item.Key,
                        value: item.Value,
                        found: true,
                        values: items)
                    : MemoryToolResponse.Failure(
                        error: "memory item was not found.",
                        operation: "get",
                        key: item.Key,
                        found: false,
                        values: items);
            }

            var foundCount = items.Count(item => item.Found);
            return foundCount == items.Length
                ? MemoryToolResponse.Success(
                    operation: "get",
                    count: foundCount,
                    values: items)
                : MemoryToolResponse.Failure(
                    error: foundCount == 0
                        ? "none of the requested memory items were found."
                        : "some requested memory items were not found.",
                    operation: "get",
                    count: foundCount,
                    values: items);
        }
    }

    private MemoryToolResponse List(int limit)
    {
        if (limit is 0 or < -1)
        {
            return MemoryToolResponse.Failure(
                operation: "list",
                error: "limit must be -1 or a positive integer.");
        }

        lock (_lock)
        {
            var model = LoadUnsafe();
            var keys = model.Items
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new MemoryKeyItem(entry.Key))
                .Take(limit == -1 ? int.MaxValue : limit)
                .ToArray();

            return MemoryToolResponse.Success(
                operation: "list",
                count: keys.Length,
                keys: keys);
        }
    }

    private MemoryToolResponse Delete(string? key)
    {
        if (!TryParseKeyList(key, out var normalizedKeys, out var error))
            return MemoryToolResponse.Failure(
                operation: "delete",
                error: error);

        lock (_lock)
        {
            var model = LoadUnsafe();
            var items = normalizedKeys
                .Select(currentKey => new MemoryDeleteItem(
                    Key: currentKey,
                    Deleted: model.Items.Remove(currentKey)))
                .ToArray();

            if (items.Any(item => item.Deleted))
                SaveUnsafe(model);

            if (items.Length == 1)
            {
                var item = items[0];
                return item.Deleted
                    ? MemoryToolResponse.Success(
                        operation: "delete",
                        key: item.Key,
                        deleted: true,
                        deletions: items)
                    : MemoryToolResponse.Failure(
                        error: "memory item was not found.",
                        operation: "delete",
                        key: item.Key,
                        deleted: false,
                        deletions: items);
            }

            var deletedCount = items.Count(item => item.Deleted);
            return deletedCount == items.Length
                ? MemoryToolResponse.Success(
                    operation: "delete",
                    count: deletedCount,
                    deletions: items)
                : MemoryToolResponse.Failure(
                    error: deletedCount == 0
                        ? "none of the requested memory items were found."
                        : "some requested memory items were not found.",
                    operation: "delete",
                    count: deletedCount,
                    deletions: items);
        }
    }

    private MemoryFileModel LoadUnsafe()
    {
        EnsureDirectoryExists();

        if (!File.Exists(_filePath))
            return new MemoryFileModel();

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new MemoryFileModel();

        try
        {
            var model = JsonSerializer.Deserialize<MemoryFileModel>(json, ReadOptions);
            return model ?? new MemoryFileModel();
        }
        catch (JsonException)
        {
            return new MemoryFileModel();
        }
    }

    private void SaveUnsafe(MemoryFileModel model)
    {
        EnsureDirectoryExists();
        var json = JsonSerializer.Serialize(model, WriteOptions);
        File.WriteAllText(_filePath, json);
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static bool TryNormalizeSingleKey(string? key, out string normalizedKey, out string error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            normalizedKey = string.Empty;
            error = "key is required for upsert.";
            return false;
        }

        normalizedKey = key.Trim();
        if (normalizedKey.Contains(',', StringComparison.Ordinal))
        {
            error = "key cannot contain commas. Use comma-separated keys only for get and delete.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseKeyList(string? key, out string[] normalizedKeys, out string error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            normalizedKeys = [];
            error = "key is required.";
            return false;
        }

        var segments = key.Split(',', StringSplitOptions.TrimEntries);
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment)))
        {
            normalizedKeys = [];
            error = "key must be a comma-separated list of non-empty values.";
            return false;
        }

        normalizedKeys = segments
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryParseOperation(string operation, out MemoryOperation normalizedOperation)
    {
        normalizedOperation = operation.Trim().ToLowerInvariant() switch
        {
            "upsert" => MemoryOperation.Upsert,
            "get" => MemoryOperation.Get,
            "list" => MemoryOperation.List,
            "delete" => MemoryOperation.Delete,
            _ => default
        };

        return normalizedOperation != MemoryOperation.Unknown;
    }

    private static string SerializeResult(MemoryToolResponse result) =>
        JsonSerializer.Serialize(result, WriteOptions);

    private sealed class MemoryFileModel
    {
        public Dictionary<string, string> Items { get; set; } = [];
    }
}

/// <summary>
/// Represents a single durable memory key returned by the list operation.
/// </summary>
/// <param name="Key">The stable identifier for the stored memory item.</param>
internal sealed record MemoryKeyItem(string Key);

/// <summary>
/// Represents the result of reading a single memory item.
/// </summary>
/// <param name="Key">The requested memory key.</param>
/// <param name="Value">The stored value when the key was found.</param>
/// <param name="Found">Whether the requested key exists in the memory store.</param>
internal sealed record MemoryValueItem(string Key, string? Value, bool Found);

/// <summary>
/// Represents the result of deleting a single memory item.
/// </summary>
/// <param name="Key">The requested memory key.</param>
/// <param name="Deleted">Whether the key existed and was deleted.</param>
internal sealed record MemoryDeleteItem(string Key, bool Deleted);

/// <summary>
/// Supported operations for the durable memory tool.
/// </summary>
internal enum MemoryOperation
{
    /// <summary>
    /// No valid operation was supplied.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Creates a new memory item or updates an existing one for the specified key.
    /// </summary>
    Upsert,

    /// <summary>
    /// Reads the value stored for a specific memory key.
    /// </summary>
    Get,

    /// <summary>
    /// Returns stored memory keys sorted by key name.
    /// </summary>
    List,

    /// <summary>
    /// Removes the memory item stored for a specific key.
    /// </summary>
    Delete
}

/// <summary>
/// Describes the outcome of a memory tool operation.
/// </summary>
internal sealed record MemoryToolResponse
{
    /// <summary>
    /// Gets a value indicating whether the requested operation succeeded.
    /// </summary>
    public required bool Ok { get; init; }

    /// <summary>
    /// Gets an error message when the operation fails validation or cannot be completed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// Gets the normalized operation name that was executed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Operation { get; init; }

    /// <summary>
    /// Gets the target memory key for single-key operations.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }

    /// <summary>
    /// Gets the stored value returned by read or write operations.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>
    /// Gets the write status for <c>upsert</c>, such as <c>created</c> or <c>updated</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    /// <summary>
    /// Gets whether a requested memory item was found by a read operation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Found { get; init; }

    /// <summary>
    /// Gets whether a requested memory item was deleted.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Deleted { get; init; }

    /// <summary>
    /// Gets the number of keys returned by a list operation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; init; }

    /// <summary>
    /// Gets the ordered collection of memory keys returned by a list operation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<MemoryKeyItem>? Keys { get; init; }

    /// <summary>
    /// Gets the per-key results returned by a get operation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<MemoryValueItem>? Values { get; init; }

    /// <summary>
    /// Gets the per-key results returned by a delete operation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<MemoryDeleteItem>? Deletions { get; init; }

    /// <summary>
    /// Creates a successful response for a completed memory tool operation.
    /// </summary>
    public static MemoryToolResponse Success(
        string operation,
        string? key = null,
        string? value = null,
        string? status = null,
        bool? found = null,
        bool? deleted = null,
        int? count = null,
        IReadOnlyList<MemoryKeyItem>? keys = null,
        IReadOnlyList<MemoryValueItem>? values = null,
        IReadOnlyList<MemoryDeleteItem>? deletions = null) =>
        new()
        {
            Ok = true,
            Operation = operation,
            Key = key,
            Value = value,
            Status = status,
            Found = found,
            Deleted = deleted,
            Count = count,
            Keys = keys,
            Values = values,
            Deletions = deletions
        };

    /// <summary>
    /// Creates a failed response for a memory tool operation.
    /// </summary>
    public static MemoryToolResponse Failure(
        string error,
        string? operation = null,
        string? key = null,
        bool? found = null,
        bool? deleted = null,
        int? count = null,
        IReadOnlyList<MemoryValueItem>? values = null,
        IReadOnlyList<MemoryDeleteItem>? deletions = null) =>
        new()
        {
            Ok = false,
            Error = error,
            Operation = operation,
            Key = key,
            Found = found,
            Deleted = deleted,
            Count = count,
            Values = values,
            Deletions = deletions
        };
}
