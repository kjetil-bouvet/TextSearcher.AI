using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace TextSearcher;

public enum HtmlSearchMode
{
    HtmlSource,
    InnerText
}

public static class AppState
{
    private static readonly string[] DefaultTextFileExtensions =
    [
        ".txt", ".log", ".csv", ".tsv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".xaml", ".md", ".ini", ".config", ".yml", ".yaml", ".sql", ".ps1", ".bat", ".cmd"
    ];
    private static readonly HashSet<string> DefaultTextFileExtensionLookup = new(
        DefaultTextFileExtensions,
        StringComparer.OrdinalIgnoreCase);

    private static readonly string SettingsFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TextSearcher");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "settings.json");

    public static ObservableCollection<string> SearchFolders { get; } = [];

    public static ObservableCollection<string> TextFileExtensions { get; } = [.. DefaultTextFileExtensions];

    public static IReadOnlyList<string> AvailableTextFileExtensions => DefaultTextFileExtensions;

    public static HtmlSearchMode HtmlSearchMode { get; set; } = HtmlSearchMode.HtmlSource;

    public static string? FolderPersistenceWarning { get; private set; }

    static AppState()
    {
        LoadSearchFolders();
    }

    public static void SaveSearchFolders()
    {
        Directory.CreateDirectory(SettingsFolderPath);

        List<string> normalizedExtensions = TextFileExtensions
            .Select(TryNormalizeTextFileExtension)
            .Where(extension => extension is not null)
            .Cast<string>()
            .Where(extension => DefaultTextFileExtensionLookup.Contains(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        AppSettings settings = new()
        {
            SearchFolders = [.. SearchFolders.Distinct(StringComparer.OrdinalIgnoreCase)],
            HtmlSearchMode = HtmlSearchMode,
            TextFileExtensions = normalizedExtensions
        };

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
        FolderPersistenceWarning = null;
    }

    private static void LoadSearchFolders()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings?.SearchFolders is null)
            {
                return;
            }

            HtmlSearchMode = settings.HtmlSearchMode;

            foreach (string folder in settings.SearchFolders.Where(folder => !string.IsNullOrWhiteSpace(folder)))
            {
                if (!SearchFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    SearchFolders.Add(folder);
                }
            }

            if (settings.TextFileExtensions is null)
            {
                return;
            }

            List<string> normalizedExtensions = settings.TextFileExtensions
                .Select(TryNormalizeTextFileExtension)
                .Where(extension => extension is not null)
                .Cast<string>()
                .Where(extension => DefaultTextFileExtensionLookup.Contains(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            TextFileExtensions.Clear();
            foreach (string extension in normalizedExtensions)
            {
                TextFileExtensions.Add(extension);
            }
        }
        catch (IOException)
        {
            FolderPersistenceWarning = "Kunne ikke lese lagrede mapper.";
        }
        catch (UnauthorizedAccessException)
        {
            FolderPersistenceWarning = "Mangler tilgang til lagrede mapper.";
        }
        catch (JsonException)
        {
            FolderPersistenceWarning = "Lagrede mapper kunne ikke tolkes.";
        }
    }

    internal static bool TryNormalizeTextFileExtension(string? extension, out string normalizedExtension)
    {
        normalizedExtension = TryNormalizeTextFileExtension(extension) ?? string.Empty;
        return !string.IsNullOrEmpty(normalizedExtension);
    }

    private static string? TryNormalizeTextFileExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        string normalizedExtension = extension.Trim();
        if (!normalizedExtension.StartsWith(".", StringComparison.Ordinal))
        {
            normalizedExtension = $".{normalizedExtension}";
        }

        return normalizedExtension.ToLowerInvariant();
    }

    private sealed class AppSettings
    {
        public List<string> SearchFolders { get; set; } = [];

        public HtmlSearchMode HtmlSearchMode { get; set; } = HtmlSearchMode.HtmlSource;

        public List<string>? TextFileExtensions { get; set; }
    }
}
