using System.Collections.ObjectModel;
using System.IO;
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
    private CancellationTokenSource? _searchCancellation;
    private string _currentQuery = string.Empty;
    private string _pendingWebFindTerm = string.Empty;

    public ObservableCollection<SearchResult> Results { get; } = [];

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
        Results.Clear();
        ClearPreview("Velg et søkeresultat for å vise filen her.");
        _searchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _searchCancellation.Token;

        SetSearchingState(true, "Søker...");

        try
        {
            List<SearchResult> results = await Task.Run(
                () => SearchService.SearchFiles(AppState.SearchFolders.ToArray(), query, cancellationToken),
                cancellationToken);

            foreach (SearchResult result in results)
            {
                Results.Add(result);
            }

            StatusTextBlock.Text = results.Count == 0
                ? "Ingen treff."
                : $"Fant {results.Count} treff.";
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
        if (ResultListView.SelectedItem is not SearchResult result)
        {
            ClearPreview("Velg et søkeresultat for å vise filen her.");
            return;
        }

        PreviewHeaderTextBlock.Text = result.FilePath;

        try
        {
            if (IsWebPageFile(result.FilePath))
            {
                ShowWebPagePreview(result.FilePath);
                return;
            }

            string content = await File.ReadAllTextAsync(result.FilePath);
            ShowPreview(content, _currentQuery);
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

    private void ShowPreview(string content, string query)
    {
        WebPagePreviewBorder.Visibility = Visibility.Collapsed;
        SourcePreviewBorder.Visibility = Visibility.Visible;
        PreviewTextBlock.Text = content;
        PreviewTextBlock.TextHighlighters.Clear();

        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        TextHighlighter highlighter = new()
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Yellow),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black)
        };

        int startIndex = 0;
        while (startIndex < content.Length)
        {
            int matchIndex = content.IndexOf(query, startIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                break;
            }

            highlighter.Ranges.Add(new TextRange(matchIndex, query.Length));
            startIndex = matchIndex + query.Length;
        }

        PreviewTextBlock.TextHighlighters.Add(highlighter);
    }

    private void ShowWebPagePreview(string filePath)
    {
        PreviewTextBlock.TextHighlighters.Clear();
        PreviewTextBlock.Text = string.Empty;
        SourcePreviewBorder.Visibility = Visibility.Collapsed;
        WebPagePreviewBorder.Visibility = Visibility.Visible;
        _pendingWebFindTerm = _currentQuery;
        PreviewWebView.Source = new Uri(filePath);
    }

    private async void PreviewWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess || string.IsNullOrEmpty(_pendingWebFindTerm) || sender.CoreWebView2 is null)
        {
            return;
        }

        string findTerm = _pendingWebFindTerm;
        _pendingWebFindTerm = string.Empty;

        CoreWebView2FindOptions findOptions = sender.CoreWebView2.Environment.CreateFindOptions();
        findOptions.FindTerm = findTerm;
        findOptions.IsCaseSensitive = false;
        findOptions.ShouldHighlightAllMatches = true;
        findOptions.ShouldMatchWord = false;
        findOptions.SuppressDefaultFindDialog = false;

        await sender.CoreWebView2.Find.StartAsync(findOptions);
    }

    private void ClearPreview(string text)
    {
        PreviewHeaderTextBlock.Text = "Preview";
        _pendingWebFindTerm = string.Empty;
        WebPagePreviewBorder.Visibility = Visibility.Collapsed;
        SourcePreviewBorder.Visibility = Visibility.Visible;
        PreviewTextBlock.TextHighlighters.Clear();
        PreviewTextBlock.Text = text;
    }

    private void SetSearchingState(bool isSearching, string status)
    {
        SearchButton.IsEnabled = !isSearching;
        CancelButton.IsEnabled = isSearching;
        StatusTextBlock.Text = status;
    }
}
