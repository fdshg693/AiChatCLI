using System.Text;

namespace AiChatCLI;

internal sealed class TextFileReader
{
    public TextFileReadResult Read(string filePath, int startLine = 1, int endLine = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath を指定してください。", nameof(filePath));

        var resolvedPath = Path.GetFullPath(filePath.Trim());
        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException("ファイルが見つかりません。", resolvedPath);

        var bytes = File.ReadAllBytes(resolvedPath);
        var decoded = Decode(bytes);
        var lines = SplitLines(decoded.Text);
        var range = NormalizeLineRange(startLine, endLine, lines.Count);
        var selectedLines = lines
            .Skip(range.StartLine - 1)
            .Take(range.EndLine - range.StartLine + 1);

        return new TextFileReadResult(
            string.Join(Environment.NewLine, selectedLines),
            decoded.EncodingName,
            lines.Count,
            range.StartLine,
            range.EndLine);
    }

    private static DecodedText Decode(byte[] bytes)
    {
        var bomDecoded = TryDecodeBom(bytes);
        if (bomDecoded is not null)
            return bomDecoded;

        try
        {
            return new DecodedText(TextEncodingDefaults.StrictUtf8.GetString(bytes), "utf-8");
        }
        catch (DecoderFallbackException)
        {
        }

        TextEncodingDefaults.RegisterCodePagesProvider();
        foreach (var encoding in GetFallbackEncodings())
        {
            try
            {
                return new DecodedText(encoding.GetString(bytes), encoding.WebName);
            }
            catch (DecoderFallbackException)
            {
            }
        }

        return new DecodedText(Encoding.UTF8.GetString(bytes), "utf-8-replacement");
    }

    private static DecodedText? TryDecodeBom(byte[] bytes)
    {
        if (StartsWith(bytes, [0xEF, 0xBB, 0xBF]))
            return new DecodedText(TextEncodingDefaults.StrictUtf8.GetString(bytes, 3, bytes.Length - 3), "utf-8-bom");

        if (StartsWith(bytes, [0xFF, 0xFE, 0x00, 0x00]))
            return new DecodedText(new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true).GetString(bytes, 4, bytes.Length - 4), "utf-32le-bom");

        if (StartsWith(bytes, [0x00, 0x00, 0xFE, 0xFF]))
            return new DecodedText(new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true).GetString(bytes, 4, bytes.Length - 4), "utf-32be-bom");

        if (StartsWith(bytes, [0xFF, 0xFE]))
            return new DecodedText(new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2), "utf-16le-bom");

        if (StartsWith(bytes, [0xFE, 0xFF]))
            return new DecodedText(new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2), "utf-16be-bom");

        return null;
    }

    private static IEnumerable<Encoding> GetFallbackEncodings()
    {
        var seen = new HashSet<int>();
        foreach (var codePage in GetFallbackCodePages())
        {
            if (!seen.Add(codePage))
                continue;

            yield return Encoding.GetEncoding(
                codePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
    }

    private static IEnumerable<int> GetFallbackCodePages()
    {
        yield return 932;

        if (OperatingSystem.IsWindows())
            yield return Encoding.GetEncoding(0).CodePage;
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.Split('\n', StringSplitOptions.None);
    }

    private static (int StartLine, int EndLine) NormalizeLineRange(int startLine, int endLine, int totalLines)
    {
        if (totalLines <= 0)
            return (1, 1);

        var normalizedStart = startLine <= 0 ? 1 : startLine;
        var normalizedEnd = endLine <= 0 ? totalLines : endLine;

        if (normalizedStart > normalizedEnd)
            throw new ArgumentOutOfRangeException(nameof(startLine), "start_line は end_line 以下を指定してください。");

        if (normalizedStart > totalLines)
            throw new ArgumentOutOfRangeException(nameof(startLine), "start_line がファイルの総行数を超えています。");

        normalizedEnd = Math.Min(normalizedEnd, totalLines);
        return (normalizedStart, normalizedEnd);
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
            return false;

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
                return false;
        }

        return true;
    }

    private sealed record DecodedText(string Text, string EncodingName);
}

internal sealed record TextFileReadResult(
    string Content,
    string EncodingName,
    int TotalLines,
    int StartLine,
    int EndLine);
