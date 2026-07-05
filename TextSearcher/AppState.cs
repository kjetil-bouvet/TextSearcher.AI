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
    private static readonly string SettingsFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TextSearcher");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "settings.json");

    public static ObservableCollection<string> SearchFolders { get; } = [];

    public static HtmlSearchMode HtmlSearchMode { get; set; } = HtmlSearchMode.HtmlSource;

    public static string? FolderPersistenceWarning { get; private set; }

    static AppState()
    {
        LoadSearchFolders();
    }

    public static void SaveSearchFolders()
    {
        Directory.CreateDirectory(SettingsFolderPath);

        AppSettings settings = new()
        {
            SearchFolders = [.. SearchFolders.Distinct(StringComparer.OrdinalIgnoreCase)],
            HtmlSearchMode = HtmlSearchMode
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

    private sealed class AppSettings
    {
        public List<string> SearchFolders { get; set; } = [];

        public HtmlSearchMode HtmlSearchMode { get; set; } = HtmlSearchMode.HtmlSource;
    }
}
