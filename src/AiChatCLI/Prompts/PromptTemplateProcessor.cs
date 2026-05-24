using System.Text;

namespace AiChatCLI;

internal class PromptTemplateProcessor : IPromptTemplateProcessor
{
    private readonly PromptTemplateManager _templateManager;
    private readonly int _maxDepth;

    public PromptTemplateProcessor(PromptTemplateManager templateManager, int maxDepth = 10)
    {
        _templateManager = templateManager;
        _maxDepth = maxDepth;
    }

    public string Process(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);

        for (var index = 0; index < input.Length;)
        {
            if (input[index] == '@' && IsTokenBoundary(input, index - 1))
            {
                var tokenEnd = index + 1;
                while (tokenEnd < input.Length && !char.IsWhiteSpace(input[tokenEnd]))
                    tokenEnd++;

                if (TryResolveAtReference(input[(index + 1)..tokenEnd], out var template))
                {
                    result.Append(template);
                    index = tokenEnd;
                    continue;
                }
            }

            result.Append(input[index]);
            index++;
        }

        return result.ToString();
    }

    private bool TryResolveAtReference(string key, out string template)
    {
        template = null!;
        if (string.IsNullOrWhiteSpace(key) || !_templateManager.TryGetTemplate(key, out var rawTemplate))
            return false;

        template = PlaceholderExpander.Expand(rawTemplate, ResolveTemplateReference, _maxDepth);
        return true;
    }

    private string? ResolveTemplateReference(string key)
    {
        return _templateManager.TryGetTemplate(key, out var template)
            ? template
            : null;
    }

    private static bool IsTokenBoundary(string input, int index)
        => index < 0 || char.IsWhiteSpace(input[index]);
}
