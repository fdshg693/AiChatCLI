namespace AiChatCLI.Commands;

public class PromptCommand : ISlashCommand
{
    private static readonly CommandHelpEntry PromptHelpEntry =
        new(
            "/prompt",
            "prompt 系コマンドの入口。配下のサブリソースを表示",
            ["/prompt", "/prompt template"]);

    private static readonly CommandHelpEntry TemplateHelpEntry =
        new(
            "/prompt template",
            "template の一覧表示・参照・更新・削除・再読み込み",
            [
                "/prompt template list",
                "/prompt template show <key>",
                "/prompt template set <key> <body...>",
                "/prompt template delete <key>",
                "/prompt template reload"
            ]);

    private static readonly CommandHelpEntry[] HelpEntryDefinitions =
    [
        PromptHelpEntry,
        TemplateHelpEntry
    ];

    private readonly PromptTemplateManager _templateManager;

    public PromptCommand(PromptTemplateManager templateManager)
    {
        _templateManager = templateManager;
    }

    public string Name => "prompt";
    public string Description => "template を操作";
    public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntryDefinitions;

    public void Execute(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            ShowPromptHelp(output);
            return;
        }

        var subresource = args[0].ToLowerInvariant();
        var subArgs = args[1..];

        switch (subresource)
        {
            case "template":
                ExecuteTemplate(subArgs, output);
                break;
            default:
                output.WriteLine($"不明な prompt サブリソース: {args[0]}");
                ShowPromptHelp(output);
                break;
        }
    }

    private static void ShowPromptHelp(TextWriter output)
    {
        CommandConventions.ShowHelpEntry(PromptHelpEntry, output);
    }

    private void ExecuteTemplate(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            ShowTemplateHelp(output);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                ListTemplates(output);
                break;
            case "show":
                ShowTemplate(args, output);
                break;
            case "set":
                SetTemplate(args, output);
                break;
            case "delete":
                DeleteTemplate(args, output);
                break;
            case "reload":
                ReloadTemplates(output);
                break;
            default:
                output.WriteLine($"不明な template アクション: {args[0]}");
                ShowTemplateHelp(output);
                break;
        }
    }

    private static void ShowTemplateHelp(TextWriter output)
    {
        CommandConventions.ShowHelpEntry(TemplateHelpEntry, output);
    }

    private void ListTemplates(TextWriter output)
    {
        var templates = _templateManager.GetTemplates();
        if (templates.Count == 0)
        {
            output.WriteLine("テンプレートが登録されていません。");
            return;
        }

        output.WriteLine("--- テンプレート一覧 ---");
        foreach (var (key, value) in templates.OrderBy(t => t.Key, StringComparer.Ordinal))
            output.WriteLine($"  @{key} : {value}");

        output.WriteLine("------------------------");
    }

    private void ShowTemplate(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /prompt template show <key>");
            return;
        }

        var key = string.Join(' ', args[1..]).Trim();
        if (string.IsNullOrEmpty(key))
        {
            output.WriteLine("キーを指定してください。");
            return;
        }

        if (!_templateManager.TryGetTemplate(key, out var value))
        {
            output.WriteLine($"キー '{key}' は登録されていません。");
            return;
        }

        output.WriteLine($"@{key}");
        output.WriteLine(value);
    }

    private void SetTemplate(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /prompt template set <key> <body...>");
            return;
        }

        var key = args[1].Trim();
        if (string.IsNullOrEmpty(key))
        {
            output.WriteLine("キーを指定してください。");
            return;
        }

        var body = args.Length == 2 ? "" : string.Join(' ', args[2..]);
        var existed = _templateManager.ContainsTemplate(key);
        if (!_templateManager.TrySetTemplate(key, body))
        {
            output.WriteLine("保存に失敗しました（ディスクへの書き込みエラーなど）。");
            return;
        }

        var action = existed ? "更新" : "追加";
        output.WriteLine($"テンプレート '{key}' を{action}し、設定済みの template 定義ファイルに保存しました。");
    }

    private void DeleteTemplate(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /prompt template delete <key>");
            return;
        }

        var key = string.Join(' ', args[1..]).Trim();
        if (string.IsNullOrEmpty(key))
        {
            output.WriteLine("キーを指定してください。");
            return;
        }

        if (!_templateManager.TryRemoveTemplate(key))
        {
            if (!_templateManager.ContainsTemplate(key))
                output.WriteLine($"キー '{key}' は登録されていません。");
            else
                output.WriteLine("削除に失敗しました（ディスクへの書き込みエラーなど）。");

            return;
        }

        output.WriteLine($"テンプレート '{key}' を削除し、設定済みの template 定義ファイルを更新しました。");
    }

    private void ReloadTemplates(TextWriter output)
    {
        if (!_templateManager.ReloadTemplatesFromDisk())
        {
            output.WriteLine("再読み込みに失敗しました（ファイルが無い、または JSON が不正など）。");
            return;
        }

        output.WriteLine("設定済みの template 定義ファイルを再読み込みしました。");
    }
}
