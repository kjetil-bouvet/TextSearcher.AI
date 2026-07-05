using System.Collections.ObjectModel;

namespace TextSearcher;

public sealed class SearchResultGroup(string filePath, IEnumerable<SearchResult> matches)
{
    public string FilePath { get; set; } = filePath;

    public ObservableCollection<SearchResult> Matches { get; set; } = [.. matches];
}

public sealed class SearchResult(string filePath, int lineNumber, string preview)
{
    public string FilePath { get; set; } = filePath;

    public int LineNumber { get; set; } = lineNumber;

    public string Preview { get; set; } = preview;

    public string DisplayText => $"Linje {LineNumber}: {Preview}";
}
