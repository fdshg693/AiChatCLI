namespace AiChatCLI.Tests;

public sealed class InteractivePromptReaderTests
{
    [Theory]
    [InlineData(ConsoleKey.V, ConsoleModifiers.Control, true)]
    [InlineData(ConsoleKey.V, ConsoleModifiers.Control | ConsoleModifiers.Shift, true)]
    [InlineData(ConsoleKey.Insert, ConsoleModifiers.Shift, true)]
    [InlineData(ConsoleKey.Insert, ConsoleModifiers.Control, false)]
    [InlineData(ConsoleKey.C, ConsoleModifiers.Control, false)]
    public void IsPasteShortcut_RecognizesSupportedShortcuts(ConsoleKey key, ConsoleModifiers modifiers, bool expected)
    {
        var keyInfo = new ConsoleKeyInfo('\0', key, (modifiers & ConsoleModifiers.Shift) != 0, (modifiers & ConsoleModifiers.Alt) != 0, (modifiers & ConsoleModifiers.Control) != 0);

        Assert.Equal(expected, InteractivePromptReader.IsPasteShortcut(keyInfo));
    }

    [Fact]
    public void NormalizePastedText_ConvertsWindowsAndClassicMacLineEndings()
    {
        var normalized = InteractivePromptReader.NormalizePastedText("a\r\nb\rc");

        Assert.Equal("a\nb\nc", normalized);
    }
}
