using System.IO;
using System.Text;
using System.Text.Unicode;

namespace TextSearcher;

internal static class TextFileReader
{
    private const int EncodingSampleSize = 64 * 1024;
    private static readonly Encoding Utf32BigEndian = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
    private static readonly Encoding[] BomEncodings =
    [
        Utf32BigEndian, Encoding.UTF32, Encoding.UTF8, Encoding.BigEndianUnicode, Encoding.Unicode
    ];
    private static readonly Encoding Windows1252;

    static TextFileReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    internal static string ReadAllText(string filePath)
    {
        return Decode(File.ReadAllBytes(filePath));
    }

    internal static async Task<string> ReadAllTextAsync(string filePath)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
        return await Task.Run(() => Decode(bytes)).ConfigureAwait(false);
    }

    private static string Decode(byte[] bytes)
    {
        Encoding encoding = DetectEncoding(bytes);
        ReadOnlySpan<byte> content = bytes;
        if (content.StartsWith(encoding.Preamble))
        {
            content = content[encoding.Preamble.Length..];
        }

        return encoding.GetString(content);
    }

    private static Encoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        foreach (Encoding encoding in BomEncodings)
        {
            if (bytes.StartsWith(encoding.Preamble))
            {
                return encoding;
            }
        }

        // Without a BOM, Latin text in UTF-16/32 has regular lanes of zero bytes.
        ReadOnlySpan<byte> sample = bytes[..Math.Min(bytes.Length, EncodingSampleSize)];
        if (bytes.Length % 4 == 0)
        {
            if (MatchesNullPattern(sample, 4, 0))
            {
                return Encoding.UTF32;
            }

            if (MatchesNullPattern(sample, 4, 3))
            {
                return Utf32BigEndian;
            }
        }

        if (bytes.Length % 2 == 0)
        {
            if (MatchesNullPattern(sample, 2, 0))
            {
                return Encoding.Unicode;
            }

            if (MatchesNullPattern(sample, 2, 1))
            {
                return Encoding.BigEndianUnicode;
            }
        }

        if (Utf8.IsValid(bytes))
        {
            return Encoding.UTF8;
        }

        // Legacy encodings are ambiguous; prefer Windows-1252, which also preserves Latin-1's Norwegian letters.
        return Windows1252;
    }

    private static bool MatchesNullPattern(ReadOnlySpan<byte> bytes, int characterSize, int valueOffset)
    {
        int characterCount = bytes.Length / characterSize;
        if (characterCount == 0)
        {
            return false;
        }

        int expectedNullCount = 0;
        int valueNullCount = 0;
        for (int index = 0; index < characterCount * characterSize; index++)
        {
            if (bytes[index] == 0)
            {
                if (index % characterSize == valueOffset)
                {
                    valueNullCount++;
                }
                else
                {
                    expectedNullCount++;
                }
            }
        }

        return expectedNullCount >= characterCount * (characterSize - 1) * 0.8
            && valueNullCount <= characterCount * 0.2;
    }
}
