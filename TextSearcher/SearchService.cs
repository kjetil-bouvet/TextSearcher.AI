using System.IO;

namespace TextSearcher;

public static class SearchService
{
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

                if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult(filePath, lineNumber, line.Trim()));
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
}
