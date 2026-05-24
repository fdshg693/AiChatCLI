using System.Text;

namespace AiChatCLI;

internal static class TextEncodingDefaults
{
    private static int _registeredCodePagesProvider;

    public static Encoding Utf8NoBom { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static Encoding StrictUtf8 { get; } = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void RegisterCodePagesProvider()
    {
        if (Interlocked.Exchange(ref _registeredCodePagesProvider, 1) == 1)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
