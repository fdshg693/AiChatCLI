using System.Text;

namespace AiChatCLI;

/// <summary>
/// Writes to two <see cref="TextWriter"/> instances (console + capture buffer).
/// </summary>
internal sealed class TeeTextWriter : TextWriter
{
    private readonly TextWriter _primary;
    private readonly TextWriter _secondary;

    public TeeTextWriter(TextWriter primary, TextWriter secondary)
    {
        _primary = primary;
        _secondary = secondary;
    }

    public override Encoding Encoding => _primary.Encoding;

    public override void Write(char value)
    {
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void Write(string? value)
    {
        if (value is null) return;
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _primary.Write(buffer, index, count);
        _secondary.Write(buffer, index, count);
    }

    public override void WriteLine(string? value)
    {
        _primary.WriteLine(value);
        _secondary.WriteLine(value);
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        _primary.Write(buffer);
        _secondary.Write(buffer);
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        _primary.WriteLine(buffer);
        _secondary.WriteLine(buffer);
    }

    public override void Flush()
    {
        _primary.Flush();
        _secondary.Flush();
    }
}
