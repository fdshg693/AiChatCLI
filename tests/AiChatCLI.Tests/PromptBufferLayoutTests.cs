namespace AiChatCLI.Tests;

public sealed class PromptBufferLayoutTests
{
    [Fact]
    public void Calculate_PreservesMultilineBufferAndTracksCursor()
    {
        var layout = PromptBufferLayout.Calculate("You> ", "abc\ndef", 7, 80);

        Assert.Equal(["You> abc", "def"], layout.Lines);
        Assert.Equal(1, layout.CursorRow);
        Assert.Equal(3, layout.CursorColumn);
    }

    [Fact]
    public void Calculate_TreatsTrailingNewlineAsStartOfNextVisualLine()
    {
        var layout = PromptBufferLayout.Calculate("You> ", "abc\n", 4, 80);

        Assert.Equal(["You> abc", string.Empty], layout.Lines);
        Assert.Equal(1, layout.CursorRow);
        Assert.Equal(0, layout.CursorColumn);
    }

    [Fact]
    public void Calculate_WrapsAfterWidePromptWidth()
    {
        var layout = PromptBufferLayout.Calculate("defaultエージェント> ", "a", 1, 22);

        Assert.Equal(["defaultエージェント> ", "a"], layout.Lines);
        Assert.Equal(1, layout.CursorRow);
        Assert.Equal(1, layout.CursorColumn);
    }
}
