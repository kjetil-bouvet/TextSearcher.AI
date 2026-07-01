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

    public static List<SearchResult> SearchFiles(
        IReadOnlyCollection<string> folders,
        string query,
        CancellationToken cancellationToken)
    {
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
                SearchFile(filePath, query, results, cancellationToken);
            }
        }

        return results;
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
        string query,
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

                int matchIndex = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (matchIndex >= 0)
                {
                    results.Add(new SearchResult(filePath, lineNumber, CreateResultPreview(line, matchIndex, query.Length)));
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
}
