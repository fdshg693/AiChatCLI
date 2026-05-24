using System.Text;

namespace AiChatCLI;

public sealed class InteractivePromptReader
{
    private readonly PromptTemplateManager _templateManager;
    private readonly IClipboardService _clipboardService;

    public InteractivePromptReader(PromptTemplateManager templateManager)
        : this(templateManager, new SystemClipboardService())
    {
    }

    internal InteractivePromptReader(PromptTemplateManager templateManager, IClipboardService clipboardService)
    {
        _templateManager = templateManager;
        _clipboardService = clipboardService;
    }

    public string? ReadLine(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        var buffer = new StringBuilder();
        var cursorIndex = 0;
        var selectionIndex = 0;
        var previousSuggestionsVisible = false;
        var renderTop = Console.CursorTop;
        var lastRenderLineCount = 0;

        while (true)
        {
            var suggestions = GetSuggestions(buffer, cursorIndex);
            if (suggestions.Count == 0)
            {
                selectionIndex = 0;
            }
            else if (!previousSuggestionsVisible)
            {
                selectionIndex = 0;
            }
            else if (selectionIndex >= suggestions.Count)
            {
                selectionIndex = suggestions.Count - 1;
            }

            lastRenderLineCount = Render(prompt, buffer, cursorIndex, suggestions, selectionIndex, renderTop, lastRenderLineCount);
            previousSuggestionsVisible = suggestions.Count > 0;

            var keyInfo = Console.ReadKey(intercept: true);
            if (IsPasteShortcut(keyInfo))
            {
                if (TryPasteFromClipboard(buffer, ref cursorIndex))
                    previousSuggestionsVisible = false;

                continue;
            }

            switch (keyInfo.Key)
            {
                case ConsoleKey.Backspace:
                    if (cursorIndex > 0)
                    {
                        buffer.Remove(cursorIndex - 1, 1);
                        cursorIndex--;
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorIndex < buffer.Length)
                        buffer.Remove(cursorIndex, 1);
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorIndex > 0)
                        cursorIndex--;
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorIndex < buffer.Length)
                        cursorIndex++;
                    break;

                case ConsoleKey.Home:
                    cursorIndex = 0;
                    break;

                case ConsoleKey.End:
                    cursorIndex = buffer.Length;
                    break;

                case ConsoleKey.UpArrow:
                    if (suggestions.Count > 0)
                        selectionIndex = (selectionIndex - 1 + suggestions.Count) % suggestions.Count;
                    break;

                case ConsoleKey.DownArrow:
                    if (suggestions.Count > 0)
                        selectionIndex = (selectionIndex + 1) % suggestions.Count;
                    break;

                case ConsoleKey.Tab:
                    if (TryApplySuggestion(buffer, ref cursorIndex, suggestions, selectionIndex))
                        previousSuggestionsVisible = false;
                    break;

                case ConsoleKey.Enter:
                    if (TryApplySuggestion(buffer, ref cursorIndex, suggestions, selectionIndex))
                    {
                        previousSuggestionsVisible = false;
                        break;
                    }

                    ClearSuggestionArea(renderTop, lastRenderLineCount);
                    Console.SetCursorPosition(0, renderTop);
                    Console.Write(prompt);
                    Console.WriteLine(ToConsoleText(buffer.ToString()));
                    return buffer.ToString();

                case ConsoleKey.Escape:
                    previousSuggestionsVisible = false;
                    selectionIndex = 0;
                    break;

                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        buffer.Insert(cursorIndex, keyInfo.KeyChar);
                        cursorIndex++;
                    }
                    break;
            }
        }
    }

    private static bool TryApplySuggestion(
        StringBuilder buffer,
        ref int cursorIndex,
        IReadOnlyList<string> suggestions,
        int selectionIndex)
    {
        if (suggestions.Count == 0 || selectionIndex < 0 || selectionIndex >= suggestions.Count)
            return false;

        InsertText(buffer, ref cursorIndex, suggestions[selectionIndex]);
        return true;
    }

    private int Render(
        string prompt,
        StringBuilder buffer,
        int cursorIndex,
        IReadOnlyList<string> suggestions,
        int selectionIndex,
        int renderTop,
        int lastRenderLineCount)
    {
        ClearSuggestionArea(renderTop, lastRenderLineCount);

        var width = GetBufferWidth();
        var layout = PromptBufferLayout.Calculate(prompt, buffer.ToString(), cursorIndex, width);
        var inputLineCount = layout.Lines.Count;
        var totalLineCount = inputLineCount + suggestions.Count;

        for (var index = 0; index < layout.Lines.Count; index++)
        {
            Console.SetCursorPosition(0, renderTop + index);
            WriteLinePadded(layout.Lines[index], width);
        }

        for (var index = 0; index < suggestions.Count; index++)
        {
            Console.SetCursorPosition(0, renderTop + inputLineCount + index);
            var marker = index == selectionIndex ? "> " : "  ";
            var line = $"{marker}@{suggestions[index]}";
            WriteLinePadded(line, width);
        }

        Console.SetCursorPosition(layout.CursorColumn, renderTop + layout.CursorRow);

        return totalLineCount;
    }

    private static void ClearSuggestionArea(int renderTop, int lineCount)
    {
        if (lineCount <= 0)
            return;

        var width = GetBufferWidth();
        for (var index = 0; index < lineCount; index++)
        {
            Console.SetCursorPosition(0, renderTop + index);
            WriteLinePadded(string.Empty, width);
        }
    }

    private static void WriteLinePadded(string text, int width)
    {
        var safeWidth = Math.Max(1, width - 1);
        var visibleText = ConsoleDisplayWidth.TrimToWidth(text, safeWidth);
        var visibleWidth = ConsoleDisplayWidth.GetWidth(visibleText);

        Console.Write(visibleText);
        Console.Write(new string(' ', Math.Max(0, safeWidth - visibleWidth)));
    }

    internal static bool IsPasteShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key is ConsoleKey.V && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
        || keyInfo.Key is ConsoleKey.Insert && (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;

    internal static string NormalizePastedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private bool TryPasteFromClipboard(StringBuilder buffer, ref int cursorIndex)
    {
        if (!_clipboardService.TryGetText(out var clipboardText) || string.IsNullOrEmpty(clipboardText))
            return false;

        InsertText(buffer, ref cursorIndex, NormalizePastedText(clipboardText));
        return true;
    }

    private static void InsertText(StringBuilder buffer, ref int cursorIndex, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        buffer.Insert(cursorIndex, text);
        cursorIndex += text.Length;
    }

    private static string ToConsoleText(string text) =>
        text.Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private IReadOnlyList<string> GetSuggestions(StringBuilder buffer, int cursorIndex)
    {
        if (!IsSuggestionTrigger(buffer, cursorIndex))
            return [];

        return _templateManager
            .GetTemplates()
            .Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSuggestionTrigger(StringBuilder buffer, int cursorIndex)
    {
        if (cursorIndex <= 0 || cursorIndex > buffer.Length)
            return false;

        if (buffer[cursorIndex - 1] != '@')
            return false;

        if (cursorIndex >= 2 && !char.IsWhiteSpace(buffer[cursorIndex - 2]))
            return false;

        return cursorIndex == buffer.Length || char.IsWhiteSpace(buffer[cursorIndex]);
    }

    private static int GetBufferWidth() => Math.Max(Console.BufferWidth, 20);
}
