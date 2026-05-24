using System.Text;

namespace AiChatCLI.Tests;

public sealed class ConsoleDisplayWidthTests
{
    [Fact]
    public void GetWidth_CountsJapanesePromptUsingConsoleColumns()
    {
        Assert.Equal(21, ConsoleDisplayWidth.GetWidth("defaultエージェント> "));
    }

    [Fact]
    public void GetWidth_UsesRequestedStringBuilderPrefix()
    {
        var buffer = new StringBuilder("abcエージ");

        Assert.Equal(5, ConsoleDisplayWidth.GetWidth(buffer, 4));
    }

    [Fact]
    public void TrimToWidth_DoesNotSplitFullWidthCharacters()
    {
        Assert.Equal("abcエ", ConsoleDisplayWidth.TrimToWidth("abcエージェント", 5));
    }
}
