namespace TextSearcher;

public sealed class SearchResult(string filePath, int lineNumber, string preview)
{
    public string FilePath { get; set; } = filePath;

    public int LineNumber { get; set; } = lineNumber;

    public string Preview { get; set; } = preview;

    public string DisplayText => $"Linje {LineNumber}: {Preview}";
}
