namespace AiChatCLI;

internal interface ICommandApprovalPrompt
{
    Task<CommandApprovalDecision> RequestApprovalAsync(string command);
}

internal sealed record CommandApprovalDecision(bool Approved, string? DenialReason)
{
    public static CommandApprovalDecision Approve() => new(true, null);

    public static CommandApprovalDecision Deny(string? reason) =>
        new(false, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
}

internal sealed class ConsoleCommandApprovalPrompt : ICommandApprovalPrompt
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly SemaphoreSlim _promptGate = new(1, 1);

    public ConsoleCommandApprovalPrompt()
        : this(Console.In, Console.Out)
    {
    }

    internal ConsoleCommandApprovalPrompt(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public async Task<CommandApprovalDecision> RequestApprovalAsync(string command)
    {
        await _promptGate.WaitAsync();
        try
        {
            return RequestApproval(command);
        }
        finally
        {
            _promptGate.Release();
        }
    }

    private CommandApprovalDecision RequestApproval(string command)
    {
        _output.WriteLine();
        _output.WriteLine("AI がコマンド実行を要求しています:");
        _output.WriteLine(command);

        while (true)
        {
            _output.Write("実行しますか? YES/NO: ");
            var answer = _input.ReadLine();
            if (IsYes(answer))
                return CommandApprovalDecision.Approve();

            if (IsNo(answer))
            {
                _output.Write("NO の理由 (任意): ");
                return CommandApprovalDecision.Deny(_input.ReadLine());
            }

            _output.WriteLine("YES または NO を入力してください。");
        }
    }

    private static bool IsYes(string? answer) =>
        string.Equals(answer?.Trim(), "YES", StringComparison.OrdinalIgnoreCase);

    private static bool IsNo(string? answer) =>
        string.Equals(answer?.Trim(), "NO", StringComparison.OrdinalIgnoreCase);
}
