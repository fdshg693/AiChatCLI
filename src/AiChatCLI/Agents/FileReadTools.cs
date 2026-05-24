using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Provides an AI-callable read-only file tool with encoding-aware text decoding.
/// </summary>
public partial class FileReadTools
{
    /// <summary>
    /// Tool name used in <c>agents.json</c> agent tool lists.
    /// </summary>
    public const string BaseToolName = "read_file";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly SessionWorkingDirectory _workingDirectory;
    private readonly TextFileReader _reader;

    internal FileReadTools(SessionWorkingDirectory workingDirectory, TextFileReader reader)
    {
        _workingDirectory = workingDirectory;
        _reader = reader;
    }

    /// <summary>
    /// Reads a local text file without running shell commands.
    /// Relative paths are resolved from the current session working directory.
    /// Use <c>start_line</c> and <c>end_line</c> to read a 1-based inclusive line range.
    /// Leave <c>end_line</c> unset or 0 to read through the end of the file.
    /// </summary>
    /// <param name="path">The absolute path or session-current-directory-relative path to read.</param>
    /// <param name="start_line">The 1-based line number to start reading from. Defaults to 1.</param>
    /// <param name="end_line">The 1-based inclusive line number to stop at. Use 0 to read to EOF.</param>
    /// <returns>A JSON string containing ok, status, path, resolvedPath, encoding, line range, content, and error fields.</returns>
    [Function]
    public Task<string> read_file(string path, int start_line = 1, int end_line = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Serialize(FileReadToolResponse.Invalid("path を指定してください。")));

        var requestedPath = path.Trim();
        string resolvedPath;
        try
        {
            resolvedPath = _workingDirectory.ResolvePath(requestedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult(Serialize(FileReadToolResponse.Invalid(
                $"path を解決できませんでした: {ex.Message}",
                requestedPath)));
        }

        try
        {
            var result = _reader.Read(resolvedPath, start_line, end_line);
            return Task.FromResult(Serialize(FileReadToolResponse.Success(
                requestedPath,
                resolvedPath,
                result.EncodingName,
                result.TotalLines,
                result.StartLine,
                result.EndLine,
                result.Content)));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult(Serialize(FileReadToolResponse.Failure(
                "file_not_found",
                "ファイルが見つかりません。",
                requestedPath,
                resolvedPath)));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Serialize(FileReadToolResponse.Failure(
                "access_denied",
                ex.Message,
                requestedPath,
                resolvedPath)));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult(Serialize(FileReadToolResponse.Failure(
                "invalid_request",
                ex.Message,
                requestedPath,
                resolvedPath)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Serialize(FileReadToolResponse.Failure(
                "io_error",
                ex.Message,
                requestedPath,
                resolvedPath)));
        }
    }

    private static string Serialize(FileReadToolResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}

internal sealed record FileReadToolResponse(
    bool Ok,
    string Status,
    string? Path,
    string? ResolvedPath,
    string? Encoding,
    int? TotalLines,
    int? StartLine,
    int? EndLine,
    string? Content,
    string? Error)
{
    public static FileReadToolResponse Success(
        string path,
        string resolvedPath,
        string encoding,
        int totalLines,
        int startLine,
        int endLine,
        string content) =>
        new(true, "completed", path, resolvedPath, encoding, totalLines, startLine, endLine, content, null);

    public static FileReadToolResponse Invalid(string error, string? path = null) =>
        new(false, "invalid_request", path, null, null, null, null, null, null, error);

    public static FileReadToolResponse Failure(
        string status,
        string error,
        string path,
        string resolvedPath) =>
        new(false, status, path, resolvedPath, null, null, null, null, null, error);
}
