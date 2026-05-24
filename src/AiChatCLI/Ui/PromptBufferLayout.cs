using System.Text;

namespace AiChatCLI;

internal sealed record PromptRenderLayout(IReadOnlyList<string> Lines, int CursorRow, int CursorColumn);

internal static class PromptBufferLayout
{
    public static PromptRenderLayout Calculate(string prompt, string buffer, int cursorIndex, int bufferWidth)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(buffer);

        var maxLineWidth = Math.Max(1, bufferWidth - 1);
        var lines = new List<string>();
        var currentLine = new StringBuilder();
        var currentWidth = 0;
        var safeCursorIndex = Math.Clamp(cursorIndex, 0, buffer.Length);
        var cursorRow = 0;
        var cursorColumn = 0;

        void CommitLine()
        {
            lines.Add(currentLine.ToString());
            currentLine.Clear();
            currentWidth = 0;
        }

        void CaptureCursor()
        {
            cursorRow = lines.Count;
            cursorColumn = currentWidth;
        }

        void AppendRune(Rune rune)
        {
            if (rune.Value == '\r')
                return;

            if (rune.Value == '\n')
            {
                CommitLine();
                return;
            }

            var runeWidth = ConsoleDisplayWidth.GetWidth(rune);
            if (currentWidth > 0 && currentWidth + runeWidth > maxLineWidth)
                CommitLine();

            currentLine.Append(rune.ToString());
            currentWidth += runeWidth;
        }

        foreach (var rune in prompt.EnumerateRunes())
            AppendRune(rune);

        if (safeCursorIndex == 0)
            CaptureCursor();

        var processedLength = 0;
        foreach (var rune in buffer.EnumerateRunes())
        {
            if (processedLength == safeCursorIndex)
                CaptureCursor();

            AppendRune(rune);
            processedLength += rune.Utf16SequenceLength;

            if (processedLength == safeCursorIndex)
                CaptureCursor();
        }

        lines.Add(currentLine.ToString());
        return new PromptRenderLayout(lines, cursorRow, cursorColumn);
    }
}
