namespace AiChatCLI;

public static class CommandConventions
{
    public const string GroupedGrammar = "/<resource> <subresource> <action> [args...]";
    public const string StandaloneGrammar = "/<resource> <action> [args...]";

    public static void ShowHelpEntry(CommandHelpEntry entry, TextWriter output)
    {
        output.WriteLine($"--- {entry.CommandPath} ---");
        output.WriteLine(entry.Description);
        foreach (var usage in entry.Usages)
            output.WriteLine($"  {usage}");

        output.WriteLine(new string('-', Math.Max(entry.CommandPath.Length + 8, 24)));
    }
}
