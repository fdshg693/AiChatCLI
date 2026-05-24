namespace AiChatCLI.Tests;

public sealed class SlashCommandHandlerTests
{
    [Fact]
    public void TryHandle_UsesProvidedWriterForRegisteredCommand()
    {
        var handler = new SlashCommandHandler();
        handler.Register(new TestSlashCommand());
        using var output = new StringWriter();

        var handled = handler.TryHandle("/demo first second", output);

        Assert.True(handled);
        Assert.Equal($"first|second{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void TryHandle_WritesUnknownCommandToProvidedWriter()
    {
        var handler = new SlashCommandHandler();
        using var output = new StringWriter();

        var handled = handler.TryHandle("/missing", output);

        Assert.True(handled);
        var text = output.ToString();
        Assert.Contains("不明なコマンド: /missing", text);
        Assert.Contains("詳しくは /help を参照してください。", text);
    }

    private sealed class TestSlashCommand : ISlashCommand
    {
        private static readonly CommandHelpEntry[] HelpEntriesDefinition =
        [
            new("/demo", "demo command", ["/demo"])
        ];

        public string Name => "demo";

        public string Description => "demo command";

        public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntriesDefinition;

        public void Execute(string[] args, TextWriter output)
        {
            output.WriteLine(string.Join("|", args));
        }
    }
}
