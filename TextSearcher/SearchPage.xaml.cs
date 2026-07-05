using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.System;

namespace TextSearcher;

public sealed partial class SearchPage : Page
{
    private const double PreviewLineHeight = 18;
    private const int PreviewScrollContextLines = 3;

    private CancellationTokenSource? _searchCancellation;
    private string _currentQuery = string.Empty;
    private string _currentPreviewFilePath = string.Empty;
    private List<string> _pendingWebHighlightTerms = [];
    private List<string> _currentSearchTerms = [];
    private readonly Dictionary<string, SearchResultGroup> _resultGroupsByFilePath = new(StringComparer.OrdinalIgnoreCase);
    private bool _webViewInitialized;

    public ObservableCollection<SearchResultGroup> Results { get; } = [];

    public SearchPage()
    {
        InitializeComponent();
        PreviewWebView.NavigationCompleted += PreviewWebView_NavigationCompleted;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await StartSearchAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await StartSearchAsync();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
    }

    private async Task StartSearchAsync()
    {
        string query = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            StatusTextBlock.Text = "Skriv inn tekst før du søker.";
            return;
        }

        if (AppState.SearchFolders.Count == 0)
        {
            StatusTextBlock.Text = "Legg til minst én mappe på adminsiden først.";
            return;
        }

        _currentQuery = query;
        _currentSearchTerms = SearchService.GetSearchTerms(query);
        _resultGroupsByFilePath.Clear();
        Results.Clear();
        ClearPreview("Velg et søkeresultat for å vise filen her.");
        _searchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _searchCancellation.Token;
        Progress<SearchResult> progress = new(AddSearchResult);

        SetSearchingState(true, "Søker...");

        try
        {
            List<SearchResultGroup> results = await Task.Run(
                () => SearchService.SearchFiles(
                    AppState.SearchFolders.ToArray(),
                    query,
                    AppState.HtmlSearchMode,
                    progress,
                    cancellationToken),
                cancellationToken);

            int matchCount = results.Sum(result => result.Matches.Count);
            StatusTextBlock.Text = results.Count == 0
                ? "Ingen treff."
                : $"Fant {matchCount} treff i {results.Count} filer.";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Søk avbrutt.";
        }
        finally
        {
            _searchCancellation.Dispose();
            _searchCancellation = null;
            SetSearchingState(false, StatusTextBlock.Text);
        }
    }

    private async void ResultListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultListView.SelectedItem is not SearchResultGroup result)
        {
            ClearPreview("Velg et søkeresultat for å vise filen her.");
            return;
        }

        await ShowResultPreviewAsync(result.FilePath, result.Matches.FirstOrDefault());
    }

    private async void MatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SearchResult match })
        {
            await ShowResultPreviewAsync(match.FilePath, match);
        }
    }

    private async Task ShowResultPreviewAsync(string filePath, SearchResult? match)
    {
        PreviewHeaderTextBlock.Text = filePath;

        bool isSameFile = filePath.Equals(_currentPreviewFilePath, StringComparison.OrdinalIgnoreCase);
        bool showHtmlAsWebPage = IsWebPageFile(filePath) && AppState.HtmlSearchMode == HtmlSearchMode.HtmlSource;

        try
        {
            if (showHtmlAsWebPage)
            {
                if (isSameFile && match is not null)
                {
                    await ScrollWebPreviewToMatchAsync(match);
                }
                else
                {
                    await ShowWebPagePreviewAsync(filePath);
                }

                return;
            }

            if (isSameFile)
            {
                if (match is not null)
                {
                    ScrollTextPreviewToLine(match.LineNumber);
                }

                return;
            }

            string content = await File.ReadAllTextAsync(filePath);
            if (IsWebPageFile(filePath) && AppState.HtmlSearchMode == HtmlSearchMode.InnerText)
            {
                content = ExtractHtmlInnerText(content);
            }

            _currentPreviewFilePath = filePath;
            ShowPreview(content, _currentQuery);
            if (match is not null)
            {
                ScrollTextPreviewToLine(match.LineNumber);
            }
        }
        catch (IOException ex)
        {
            ClearPreview($"Kunne ikke lese filen: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearPreview($"Mangler tilgang til filen: {ex.Message}");
        }
    }

    private static bool IsWebPageFile(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractHtmlInnerText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string withoutTags = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(withoutTags);
    }

    private void ShowPreview(string content, string query)
    {
        WebPagePreviewBorder.Visibility = Visibility.Collapsed;
        SourcePreviewBorder.Visibility = Visibility.Visible;
        PreviewScrollViewer.ChangeView(null, 0, null);
        PreviewTextBlock.Text = content;
        PreviewTextBlock.TextHighlighters.Clear();

        if (string.IsNullOrEmpty(query) || _currentSearchTerms.Count == 0)
        {
            return;
        }

        TextHighlighter highlighter = new()
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Yellow),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black)
        };

        foreach (string term in _currentSearchTerms)
        {
            int startIndex = 0;
            while (startIndex < content.Length)
            {
                int matchIndex = content.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                highlighter.Ranges.Add(new TextRange(matchIndex, term.Length));
                startIndex = matchIndex + term.Length;
            }
        }

        PreviewTextBlock.TextHighlighters.Add(highlighter);
    }

    private void ScrollTextPreviewToLine(int lineNumber)
    {
        PreviewTextBlock.UpdateLayout();
        double targetOffset = Math.Max(0, (lineNumber - PreviewScrollContextLines) * PreviewLineHeight);
        PreviewScrollViewer.ChangeView(null, targetOffset, null);
    }

    private async Task ShowWebPagePreviewAsync(string filePath)
    {
        PreviewTextBlock.TextHighlighters.Clear();
        PreviewTextBlock.Text = string.Empty;
        SourcePreviewBorder.Visibility = Visibility.Collapsed;
        WebPagePreviewBorder.Visibility = Visibility.Visible;
        _pendingWebHighlightTerms = [.. _currentSearchTerms];

        string html;
        try
        {
            html = await File.ReadAllTextAsync(filePath);
        }
        catch (IOException ex)
        {
            ClearPreview($"Kunne ikke lese filen: {ex.Message}");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearPreview($"Mangler tilgang til filen: {ex.Message}");
            return;
        }

        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        string sanitizedHtml = HtmlSanitizer.Sanitize(html, directory);

        await PreviewWebView.EnsureCoreWebView2Async();

        if (!_webViewInitialized)
        {
            //PreviewWebView.CoreWebView2.Settings.IsScriptEnabled = false;
            //PreviewWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            //PreviewWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            //PreviewWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            _webViewInitialized = true;
        }

        _currentPreviewFilePath = filePath;
        PreviewWebView.NavigateToString(sanitizedHtml);
    }

    private async void PreviewWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess || _pendingWebHighlightTerms.Count == 0 || sender.CoreWebView2 is null)
        {
            return;
        }

        string script = CreateWebHighlightScript(_pendingWebHighlightTerms);
        _pendingWebHighlightTerms = [];

        await sender.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async Task ScrollWebPreviewToMatchAsync(SearchResult match)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        SearchResultGroup? group = Results.FirstOrDefault(g =>
            g.FilePath.Equals(match.FilePath, StringComparison.OrdinalIgnoreCase));
        int matchIndex = group?.Matches.IndexOf(match) ?? 0;

        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
            $$"""
            (function() {
                const marks = document.querySelectorAll('mark.text-searcher-highlight');
                const idx = {{matchIndex}};
                if (idx >= 0 && idx < marks.length) {
                    marks[idx].scrollIntoView({ block: 'center' });
                }
            })();
            """);
    }

    private void ClearPreview(string text)
    {
        PreviewHeaderTextBlock.Text = "Preview";
        _currentPreviewFilePath = string.Empty;
        _pendingWebHighlightTerms = [];
        WebPagePreviewBorder.Visibility = Visibility.Collapsed;
        SourcePreviewBorder.Visibility = Visibility.Visible;
        PreviewTextBlock.TextHighlighters.Clear();
        PreviewTextBlock.Text = text;
    }

    private static string CreateWebHighlightScript(IReadOnlyCollection<string> terms)
    {
        string termsJson = JsonSerializer.Serialize(terms.Where(term => !string.IsNullOrWhiteSpace(term)));
        return """
            (function () {
                const terms = TERMS_JSON_PLACEHOLDER;
                const className = 'text-searcher-highlight';
                const styleId = 'text-searcher-highlight-style';

                document.querySelectorAll('mark.' + className).forEach((mark) => {
                    mark.replaceWith(document.createTextNode(mark.textContent));
                });

                if (!document.getElementById(styleId)) {
                    const style = document.createElement('style');
                    style.id = styleId;
                    style.textContent = 'mark.' + className + '{background:yellow;color:black;padding:0;}';
                    document.head.appendChild(style);
                }

                const escapedTerms = terms
                    .filter((term) => term && term.trim().length > 0)
                    .sort((left, right) => right.length - left.length)
                    .map((term) => term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

                if (escapedTerms.length === 0 || !document.body) {
                    return;
                }

                const pattern = new RegExp(escapedTerms.join('|'), 'gi');
                const skippedTags = new Set(['SCRIPT', 'STYLE', 'TEXTAREA', 'INPUT', 'NOSCRIPT']);
                const walker = document.createTreeWalker(
                    document.body,
                    NodeFilter.SHOW_TEXT,
                    {
                        acceptNode: (node) => {
                            const parent = node.parentElement;
                            if (!parent || skippedTags.has(parent.tagName) || parent.closest('mark.' + className)) {
                                return NodeFilter.FILTER_REJECT;
                            }

                            pattern.lastIndex = 0;
                            return pattern.test(node.nodeValue)
                                ? NodeFilter.FILTER_ACCEPT
                                : NodeFilter.FILTER_REJECT;
                        }
                    });

                const nodes = [];
                while (walker.nextNode()) {
                    nodes.push(walker.currentNode);
                }

                nodes.forEach((node) => {
                    const fragment = document.createDocumentFragment();
                    const text = node.nodeValue;
                    let lastIndex = 0;
                    pattern.lastIndex = 0;

                    for (const match of text.matchAll(pattern)) {
                        if (match.index > lastIndex) {
                            fragment.appendChild(document.createTextNode(text.slice(lastIndex, match.index)));
                        }

                        const mark = document.createElement('mark');
                        mark.className = className;
                        mark.textContent = match[0];
                        fragment.appendChild(mark);
                        lastIndex = match.index + match[0].length;
                    }

                    if (lastIndex < text.length) {
                        fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
                    }

                    node.replaceWith(fragment);
                });

                const firstMark = document.querySelector('mark.' + className);
                if (firstMark) {
                    firstMark.scrollIntoView({ block: 'center' });
                }
            })();
            """.Replace("TERMS_JSON_PLACEHOLDER", termsJson, StringComparison.Ordinal);
    }

    private void SetSearchingState(bool isSearching, string status)
    {
        SearchButton.IsEnabled = !isSearching;
        CancelButton.IsEnabled = isSearching;
        StatusTextBlock.Text = status;
    }

    private void AddSearchResult(SearchResult result)
    {
        if (!_resultGroupsByFilePath.TryGetValue(result.FilePath, out SearchResultGroup? group))
        {
            group = new SearchResultGroup(result.FilePath, []);
            _resultGroupsByFilePath[result.FilePath] = group;
            Results.Add(group);
        }

        group.Matches.Add(result);
    }
}
