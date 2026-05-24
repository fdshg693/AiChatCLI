using System.Text.RegularExpressions;

namespace AiChatCLI;

internal static class PlaceholderExpander
{
    private static readonly Regex s_referencePattern = new(@"%([^%\s]+)%");

    public static string Expand(string text, Func<string, string?> resolveValue, int maxDepth)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return ExpandCore(text, resolveValue, Math.Max(0, maxDepth), 0);
    }

    private static string ExpandCore(string text, Func<string, string?> resolveValue, int maxDepth, int depth)
    {
        if (depth >= maxDepth || string.IsNullOrEmpty(text))
            return text;

        return s_referencePattern.Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            var value = resolveValue(key);
            if (value is null)
                return match.Value;

            return ExpandCore(value, resolveValue, maxDepth, depth + 1);
        });
    }
}
