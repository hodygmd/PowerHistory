namespace PowerHistory.Infrastructure.History;

using System.Text;
using PowerHistory.Core.Models;

/// <summary>
/// Decodes and encodes history files while preserving the original shape:
/// encoding, byte order mark, line terminator and trailing newline.
/// </summary>
internal static class HistoryFileFormat
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    internal sealed record Decoded(IReadOnlyList<string> Lines, HistoryFormat Format);

    public static Decoded Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var (encoding, preambleLength) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);

        var newLine = DetectNewLine(text);
        var endsWithNewLine = text.Length > 0 && text.EndsWith(newLine, StringComparison.Ordinal);

        if (text.Length == 0)
        {
            return new Decoded([], new HistoryFormat(newLine, preambleLength > 0, endsWithNewLine));
        }

        var parts = text.Split(newLine, StringSplitOptions.None);
        var lines = new List<string>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            // A trailing terminator produces an empty last part; it is not a line of its own.
            if (i == parts.Length - 1 && endsWithNewLine && parts[i].Length == 0)
            {
                break;
            }

            lines.Add(parts[i]);
        }

        return new Decoded(lines, new HistoryFormat(newLine, preambleLength > 0, endsWithNewLine));
    }

    public static byte[] Encode(IReadOnlyList<string> lines, HistoryFormat format)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(format);

        var text = string.Join(format.NewLine, lines);
        if (format.EndsWithNewLine && lines.Count > 0)
        {
            text += format.NewLine;
        }

        var body = new UTF8Encoding(false).GetBytes(text);
        if (!format.HasBom)
        {
            return body;
        }

        var result = new byte[Utf8Bom.Length + body.Length];
        Utf8Bom.CopyTo(result, 0);
        body.CopyTo(result, Utf8Bom.Length);
        return result;
    }

    private static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(false), 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        return (new UTF8Encoding(false), 0);
    }

    private static string DetectNewLine(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                return i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";
            }

            if (text[i] == '\n')
            {
                return "\n";
            }
        }

        return "\n";
    }
}
