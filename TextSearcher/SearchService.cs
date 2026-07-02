using System.IO;

namespace TextSearcher;

public static class SearchService
{
    private const int ResultContextLength = 80;

    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".csv", ".tsv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".xaml", ".md", ".ini", ".config", ".yml", ".yaml", ".sql", ".ps1", ".bat", ".cmd"
    };

    public static List<SearchResultGroup> SearchFiles(
        IReadOnlyCollection<string> folders,
        string query,
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
                SearchFile(filePath, expression, results, cancellationToken);
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
        ICollection<SearchResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            using StreamReader reader = new(filePath, detectEncodingFromByteOrderMarks: true);
            int lineNumber = 0;

            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNumber++;

                SearchMatch? match = expression.FindMatch(line);
                if (match is not null)
                {
                    results.Add(new SearchResult(filePath, lineNumber, CreateResultPreview(line, match.Index, match.Length)));
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string CreateResultPreview(string line, int matchIndex, int queryLength)
    {
        int startIndex = Math.Max(0, matchIndex - ResultContextLength);
        int endIndex = Math.Min(line.Length, matchIndex + queryLength + ResultContextLength);
        string preview = line[startIndex..endIndex].Trim();

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

        public SearchMatch? FindMatch(string line)
        {
            if (!Groups.Any(group => group.All(term => line.Contains(term, StringComparison.OrdinalIgnoreCase))))
            {
                return null;
            }

            return Terms
                .Select(term => new SearchMatch(line.IndexOf(term, StringComparison.OrdinalIgnoreCase), term.Length))
                .Where(match => match.Index >= 0)
                .OrderBy(match => match.Index)
                .FirstOrDefault();
        }

        private static bool IsOperator(string token)
        {
            return token.Equals("AND", StringComparison.OrdinalIgnoreCase)
                || token.Equals("OR", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record SearchMatch(int Index, int Length);
}
