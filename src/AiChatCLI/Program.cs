using AiChatCLI;

TextEncodingDefaults.RegisterCodePagesProvider();

var paths = AppPaths.Discover("AiChatCLI.csproj");
using var composition = AppComposition.Create(paths);

composition.ThreadSessionManager.Initialize();
await composition.ChatLoop.RunAsync(composition.Config.Model);
