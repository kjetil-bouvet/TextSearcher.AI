using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace TextSearcher;

public static class SearchService
{
    private const int ResultContextLength = 80;
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".csv", ".tsv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".xaml", ".md", ".ini", ".config", ".yml", ".yaml", ".sql", ".ps1", ".bat", ".cmd"
    };

    public static List<SearchResultGroup> SearchFiles(
        IReadOnlyCollection<string> folders,
        string query,
        HtmlSearchMode htmlSearchMode,
        IProgress<SearchResult>? progress,
        CancellationToken cancellationToken)
    {
        SearchExpression expression = SearchExpression.Parse(query);
        List<SearchResult> results = [];

        foreach (string folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string filePath in EnumerateTextFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SearchFile(filePath, expression, htmlSearchMode, results, progress, cancellationToken);
            }
        }

        return results
            .GroupBy(result => result.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SearchResultGroup(group.Key, group))
            .ToList();
    }

    internal static List<string> GetSearchTerms(string query)
    {
        return SearchExpression.Parse(query).Terms;
    }

    private static IEnumerable<string> EnumerateTextFiles(string folder)
    {
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };

        return Directory
            .EnumerateFiles(folder, "*", options)
            .Where(filePath => TextFileExtensions.Contains(Path.GetExtension(filePath)));
    }

    private static void SearchFile(
        string filePath,
        SearchExpression expression,
        HtmlSearchMode htmlSearchMode,
        ICollection<SearchResult> results,
        IProgress<SearchResult>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            using StreamReader reader = new(filePath, detectEncodingFromByteOrderMarks: true);
            string fileContent = reader.ReadToEnd();

            string searchableContent = fileContent;
            if (htmlSearchMode == HtmlSearchMode.InnerText && IsHtmlFile(filePath))
            {
                searchableContent = ExtractHtmlInnerText(fileContent);
            }

            List<SearchMatch> matches = expression.FindMatches(searchableContent);
            if (matches.Count == 0)
            {
                return;
            }

            int lineNumber = 1;
            int scannedIndex = 0;
            foreach (SearchMatch match in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (scannedIndex < match.Index && scannedIndex < searchableContent.Length)
                {
                    if (searchableContent[scannedIndex] == '\n')
                    {
                        lineNumber++;
                    }

                    scannedIndex++;
                }

                SearchResult result = new(
                    filePath,
                    lineNumber,
                    CreateResultPreview(searchableContent, match.Index, match.Length));
                results.Add(result);
                progress?.Report(result);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsHtmlFile(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractHtmlInnerText(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        string withoutTags = HtmlTagRegex.Replace(line, " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string CreateResultPreview(string line, int matchIndex, int queryLength)
    {
        int startIndex = Math.Max(0, matchIndex - ResultContextLength);
        int endIndex = Math.Min(line.Length, matchIndex + queryLength + ResultContextLength);
        string preview = line[startIndex..endIndex]
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (startIndex > 0)
        {
            preview = "..." + preview;
        }

        if (endIndex < line.Length)
        {
            preview += "...";
        }

        return preview;
    }

    private sealed class SearchExpression
    {
        private SearchExpression(List<List<string>> groups)
        {
            Groups = groups;
            Terms = groups.SelectMany(group => group).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public List<string> Terms { get; }

        private List<List<string>> Groups { get; }

        public static SearchExpression Parse(string query)
        {
            string trimmedQuery = query.Trim();
            string[] tokens = trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool hasOperator = tokens.Any(IsOperator);

            if (!hasOperator)
            {
                return new SearchExpression([[trimmedQuery]]);
            }

            List<List<string>> groups = [[]];

            foreach (string token in tokens)
            {
                if (token.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    if (groups[^1].Count > 0)
                    {
                        groups.Add([]);
                    }

                    continue;
                }

                if (token.Equals("AND", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                groups[^1].Add(token);
            }

            groups = groups.Where(group => group.Count > 0).ToList();
            return groups.Count == 0
                ? new SearchExpression([[trimmedQuery]])
                : new SearchExpression(groups);
        }

        public List<SearchMatch> FindMatches(string content)
        {
            List<List<string>> matchingGroups = Groups
                .Where(group => group.All(term => content.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matchingGroups.Count == 0)
            {
                return [];
            }

            List<string> matchingTerms = matchingGroups
                .SelectMany(group => group)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<SearchMatch> matches = [];
            foreach (string term in matchingTerms)
            {
                int startIndex = 0;
                while (startIndex < content.Length)
                {
                    int matchIndex = content.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    matches.Add(new SearchMatch(matchIndex, term.Length));
                    startIndex = matchIndex + term.Length;
                }
            }

            return matches
                .GroupBy(match => new { match.Index, match.Length })
                .Select(group => group.First())
                .OrderBy(match => match.Index)
                .ToList();
        }

        private static bool IsOperator(string token)
        {
            return token.Equals("AND", StringComparison.OrdinalIgnoreCase)
                || token.Equals("OR", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record SearchMatch(int Index, int Length);
}
