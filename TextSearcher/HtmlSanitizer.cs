using System.IO;
using System.Text.RegularExpressions;

namespace TextSearcher;

internal static partial class HtmlSanitizer
{
    [GeneratedRegex(@"<script\b[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlockRegex();

    [GeneratedRegex(@"<noscript\b[^>]*>[\s\S]*?</noscript>", RegexOptions.IgnoreCase)]
    private static partial Regex NoScriptBlockRegex();

    [GeneratedRegex(@"<(iframe|object|embed|frame|frameset)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementPairedRegex();

    [GeneratedRegex(@"<(iframe|object|embed|frame|frameset)\b[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementVoidRegex();

    [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BaseTagRegex();

    [GeneratedRegex(@"<meta\b[^>]*http-equiv\s*=\s*[""']?\s*(refresh|set-cookie)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRefreshRegex();

    [GeneratedRegex(@"\bon\w+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]*)", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"""javascript:[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptDoubleQuoteRegex();

    [GeneratedRegex(@"'javascript:[^']*'", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptSingleQuoteRegex();

    [GeneratedRegex(@"src\s*=\s*""data:[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex DataUrlDoubleQuoteRegex();

    [GeneratedRegex(@"src\s*=\s*'data:[^']*'", RegexOptions.IgnoreCase)]
    private static partial Regex DataUrlSingleQuoteRegex();

    [GeneratedRegex(@"<head\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadOpenTagRegex();

    [GeneratedRegex(@"<html\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlOpenTagRegex();

    public static string Sanitize(string html, string sourceDirectory)
    {
        html = ScriptBlockRegex().Replace(html, string.Empty);
        html = NoScriptBlockRegex().Replace(html, string.Empty);
        html = BlockElementPairedRegex().Replace(html, string.Empty);
        html = BlockElementVoidRegex().Replace(html, string.Empty);
        html = BaseTagRegex().Replace(html, string.Empty);
        html = MetaRefreshRegex().Replace(html, string.Empty);
        html = EventHandlerRegex().Replace(html, string.Empty);
        html = JavaScriptDoubleQuoteRegex().Replace(html, "\"#\"");
        html = JavaScriptSingleQuoteRegex().Replace(html, "'#'");
        html = DataUrlDoubleQuoteRegex().Replace(html, "src=\"\"");
        html = DataUrlSingleQuoteRegex().Replace(html, "src=''");

        string baseTag = BuildBaseTag(sourceDirectory);
        html = InjectBaseTag(html, baseTag);

        return html;
    }

    private static string BuildBaseTag(string directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        string normalised = directory.Replace('\\', '/');
        if (!normalised.EndsWith('/'))
        {
            normalised += '/';
        }

        string href = "file:///" + string.Join("/", normalised.Split('/').Select(Uri.EscapeDataString)).TrimStart('/');
        return $"<base href=\"{href}\">";
    }

    private static string InjectBaseTag(string html, string baseTag)
    {
        if (string.IsNullOrEmpty(baseTag))
        {
            return html;
        }

        Match head = HeadOpenTagRegex().Match(html);
        if (head.Success)
        {
            return html.Insert(head.Index + head.Length, baseTag);
        }

        Match htmlTag = HtmlOpenTagRegex().Match(html);
        if (htmlTag.Success)
        {
            return html.Insert(htmlTag.Index + htmlTag.Length, $"<head>{baseTag}</head>");
        }

        return baseTag + html;
    }
}
