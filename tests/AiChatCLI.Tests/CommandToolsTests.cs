using System.Text.Json;

namespace AiChatCLI.Tests;

public sealed class CommandToolsTests
{
    [Fact]
    public async Task ApprovalPrompt_ReturnsApprovedForYes()
    {
        using var input = new StringReader("YES\n");
        using var output = new StringWriter();
        var prompt = new ConsoleCommandApprovalPrompt(input, output);

        var decision = await prompt.RequestApprovalAsync("dotnet --info");

        Assert.True(decision.Approved);
        Assert.Null(decision.DenialReason);
        Assert.Contains("dotnet --info", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalPrompt_ReturnsDeniedWithoutReasonForEmptyReason()
    {
        using var input = new StringReader("NO\n\n");
        using var output = new StringWriter();
        var prompt = new ConsoleCommandApprovalPrompt(input, output);

        var decision = await prompt.RequestApprovalAsync("Remove-Item important.txt");

        Assert.False(decision.Approved);
        Assert.Null(decision.DenialReason);
    }

    [Fact]
    public async Task ApprovalPrompt_ReturnsDeniedWithReasonWhenProvided()
    {
        using var input = new StringReader("NO\n危険なコマンドです\n");
        using var output = new StringWriter();
        var prompt = new ConsoleCommandApprovalPrompt(input, output);

        var decision = await prompt.RequestApprovalAsync("Remove-Item important.txt");

        Assert.False(decision.Approved);
        Assert.Equal("危険なコマンドです", decision.DenialReason);
    }

    [Fact]
    public async Task Command_ReturnsDenialAndDoesNotStartExecutor()
    {
        var executor = new RecordingCommandExecutor();
        var tools = new CommandTools(new StubApprovalPrompt(CommandApprovalDecision.Deny("危険です")), executor);

        var result = await tools.command("Remove-Item important.txt");

        Assert.Equal(0, executor.CallCount);
        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("denied", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.GetProperty("denied").GetBoolean());
        Assert.Equal("危険です", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Command_ReturnsDenialWithoutReasonWhenReasonIsEmpty()
    {
        var tools = new CommandTools(
            new StubApprovalPrompt(CommandApprovalDecision.Deny(null)),
            new RecordingCommandExecutor());

        var result = await tools.command("echo hi");

        using var json = JsonDocument.Parse(result);
        Assert.Equal("denied", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task Command_ReturnsExecutionResultWhenApproved()
    {
        var executor = new RecordingCommandExecutor(new CommandExecutionResult(0, "hello\n", string.Empty, false));
        var tools = new CommandTools(new StubApprovalPrompt(CommandApprovalDecision.Approve()), executor);

        var result = await tools.command("echo hello", timeout_seconds: 30);

        Assert.Equal(1, executor.CallCount);
        Assert.Equal("echo hello", executor.LastCommand);
        Assert.Equal(TimeSpan.FromSeconds(30), executor.LastTimeout);
        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("denied").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("hello\n", json.RootElement.GetProperty("stdout").GetString());
    }

    [Fact]
    public async Task Command_SerializesUnicodeOutputWithoutEscaping()
    {
        var executor = new RecordingCommandExecutor(new CommandExecutionResult(0, "日本語テスト\n", string.Empty, false));
        var tools = new CommandTools(new StubApprovalPrompt(CommandApprovalDecision.Approve()), executor);

        var result = await tools.command("echo unicode");

        Assert.Contains("日本語テスト", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u65e5", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalCommandExecutor_RunsCommandAndCapturesOutput()
    {
        var executor = new LocalCommandExecutor();
        var command = OperatingSystem.IsWindows()
            ? "Write-Output 'AiChatCLI_Command_Test'"
            : "printf 'AiChatCLI_Command_Test\\n'";

        var result = await executor.ExecuteAsync(command, TimeSpan.FromSeconds(10));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("AiChatCLI_Command_Test", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCommandExecutor_PreservesUnicodeOutputOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var executor = new LocalCommandExecutor();

        var result = await executor.ExecuteAsync("Write-Output '日本語テスト'", TimeSpan.FromSeconds(10));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("日本語テスト", result.Stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 120)]
    [InlineData(0, 120)]
    [InlineData(30, 30)]
    [InlineData(1000, 600)]
    public void NormalizeTimeoutSeconds_UsesDefaultAndCap(int value, int expected)
    {
        Assert.Equal(expected, CommandTools.NormalizeTimeoutSeconds(value));
    }

    private sealed class StubApprovalPrompt(CommandApprovalDecision decision) : ICommandApprovalPrompt
    {
        public Task<CommandApprovalDecision> RequestApprovalAsync(string command) =>
            Task.FromResult(decision);
    }

    private sealed class RecordingCommandExecutor(
        CommandExecutionResult? result = null) : ICommandExecutor
    {
        public int CallCount { get; private set; }

        public string? LastCommand { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public Task<CommandExecutionResult> ExecuteAsync(string command, TimeSpan timeout)
        {
            CallCount++;
            LastCommand = command;
            LastTimeout = timeout;
            return Task.FromResult(result ?? new CommandExecutionResult(0, string.Empty, string.Empty, false));
        }
    }
}
