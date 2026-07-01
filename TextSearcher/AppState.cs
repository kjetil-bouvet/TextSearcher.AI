using System.Collections.ObjectModel;

namespace TextSearcher;

public static class AppState
{
    public static ObservableCollection<string> SearchFolders { get; } = [];
}
