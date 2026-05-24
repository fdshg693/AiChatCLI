using System.Globalization;
using System.Text;

namespace AiChatCLI;

internal static class ConsoleDisplayWidth
{
    public static int GetWidth(Rune rune) => GetRuneWidth(rune);

    public static int GetWidth(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return GetWidth(text.AsSpan());
    }

    public static int GetWidth(ReadOnlySpan<char> text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
            width += GetRuneWidth(rune);

        return width;
    }

    public static int GetWidth(StringBuilder buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var safeLength = Math.Clamp(length, 0, buffer.Length);
        return safeLength == 0
            ? 0
            : GetWidth(buffer.ToString(0, safeLength));
    }

    public static string TrimToWidth(string text, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxWidth <= 0 || text.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var currentWidth = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var runeWidth = GetRuneWidth(rune);
            if (currentWidth + runeWidth > maxWidth)
                break;

            builder.Append(rune.ToString());
            currentWidth += runeWidth;
        }

        return builder.ToString();
    }

    private static int GetRuneWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark)
        {
            return 0;
        }

        return IsWideRune(rune) ? 2 : 1;
    }

    private static bool IsWideRune(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x1100 and <= 0x115F
            or 0x2329
            or 0x232A
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE10 and <= 0xFE19
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1FAFF
            or >= 0x20000 and <= 0x2FFFD
            or >= 0x30000 and <= 0x3FFFD;
    }
}
