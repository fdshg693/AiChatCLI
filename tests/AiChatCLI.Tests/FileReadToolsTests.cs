using System.Text;
using System.Text.Json;

namespace AiChatCLI.Tests;

public sealed class FileReadToolsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public FileReadToolsTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ReadFile_ResolvesRelativePathFromSessionWorkingDirectoryAndReadsLineRange()
    {
        var sessionDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "session")).FullName;
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(sessionDirectory, "notes")).FullName;
        var filePath = Path.Combine(nestedDirectory, "sample.txt");
        File.WriteAllText(filePath, "line 1\n日本語 line 2\nline 3\nline 4", TextEncodingDefaults.Utf8NoBom);
        var tools = CreateTools(sessionDirectory);

        var result = await tools.read_file(Path.Combine("notes", "sample.txt"), start_line: 2, end_line: 3);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(Path.GetFullPath(filePath), json.RootElement.GetProperty("resolvedPath").GetString());
        Assert.Equal("utf-8", json.RootElement.GetProperty("encoding").GetString());
        Assert.Equal(4, json.RootElement.GetProperty("totalLines").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("startLine").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("endLine").GetInt32());
        Assert.Equal(
            "日本語 line 2" + Environment.NewLine + "line 3",
            json.RootElement.GetProperty("content").GetString());
        Assert.DoesNotContain("\\u65e5", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadFile_AcceptsAbsolutePath()
    {
        var filePath = Path.Combine(_tempRoot, "absolute.txt");
        File.WriteAllText(filePath, "absolute content", TextEncodingDefaults.Utf8NoBom);
        var tools = CreateTools(Path.Combine(_tempRoot, "other"));

        var result = await tools.read_file(filePath);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(Path.GetFullPath(filePath), json.RootElement.GetProperty("resolvedPath").GetString());
        Assert.Equal("absolute content", json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ReadFile_DetectsUtf8Bom()
    {
        var filePath = Path.Combine(_tempRoot, "utf8-bom.txt");
        File.WriteAllText(filePath, "bom 日本語", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var tools = CreateTools(_tempRoot);

        var result = await tools.read_file("utf8-bom.txt");

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("utf-8-bom", json.RootElement.GetProperty("encoding").GetString());
        Assert.Equal("bom 日本語", json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ReadFile_FallsBackToCp932WhenUtf8IsInvalid()
    {
        TextEncodingDefaults.RegisterCodePagesProvider();
        var filePath = Path.Combine(_tempRoot, "cp932.txt");
        File.WriteAllBytes(filePath, Encoding.GetEncoding(932).GetBytes("日本語CP932\n二行目"));
        var tools = CreateTools(_tempRoot);

        var result = await tools.read_file("cp932.txt");

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("shift_jis", json.RootElement.GetProperty("encoding").GetString());
        Assert.Equal(
            "日本語CP932" + Environment.NewLine + "二行目",
            json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ReadFile_ReturnsFileNotFoundForMissingFile()
    {
        var tools = CreateTools(_tempRoot);

        var result = await tools.read_file("missing.txt");

        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("file_not_found", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("ファイルが見つかりません", json.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_ReturnsInvalidRequestForInvalidLineRange()
    {
        var filePath = Path.Combine(_tempRoot, "range.txt");
        File.WriteAllText(filePath, "line 1\nline 2", TextEncodingDefaults.Utf8NoBom);
        var tools = CreateTools(_tempRoot);

        var result = await tools.read_file("range.txt", start_line: 3);

        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_request", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("start_line", json.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_ExposesExpectedFunctionName()
    {
        var tools = CreateTools(_tempRoot);

        Assert.Equal(FileReadTools.BaseToolName, tools.read_fileFunctionContract.Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static FileReadTools CreateTools(string sessionDirectory) =>
        new(new SessionWorkingDirectory(sessionDirectory), new TextFileReader());
}
