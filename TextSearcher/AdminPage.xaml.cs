using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TextSearcher;

public sealed partial class AdminPage : Page
{
    public ObservableCollection<string> SearchFolders => AppState.SearchFolders;

    public AdminPage()
    {
        InitializeComponent();
        UpdateStatus();
    }

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null || SearchFolders.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SearchFolders.Add(folder.Path);
        SaveSearchFolders();
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (FolderListView.SelectedItem is string folder)
        {
            SearchFolders.Remove(folder);
            SaveSearchFolders();
        }
    }

    private void SaveSearchFolders()
    {
        try
        {
            AppState.SaveSearchFolders();
            UpdateStatus();
        }
        catch (System.IO.IOException ex)
        {
            StatusTextBlock.Text = $"Kunne ikke lagre mappene: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusTextBlock.Text = $"Mangler tilgang til å lagre mappene: {ex.Message}";
        }
    }

    private void UpdateStatus()
    {
        if (!string.IsNullOrEmpty(AppState.FolderPersistenceWarning))
        {
            StatusTextBlock.Text = AppState.FolderPersistenceWarning;
            return;
        }

        StatusTextBlock.Text = SearchFolders.Count == 0
            ? "Ingen mapper valgt."
            : $"{SearchFolders.Count} mappe(r) valgt.";
    }
}
