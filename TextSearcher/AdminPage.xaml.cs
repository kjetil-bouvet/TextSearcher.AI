using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TextSearcher;

public sealed partial class AdminPage : Page
{
    public ObservableCollection<string> SearchFolders => AppState.SearchFolders;
    public ObservableCollection<string> TextFileExtensions => AppState.TextFileExtensions;

    public AdminPage()
    {
        InitializeComponent();
        BuildExtensionCheckBoxes();
        SetHtmlSearchModeSelection();
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
        SaveSettings();
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (FolderListView.SelectedItem is string folder)
        {
            SearchFolders.Remove(folder);
            SaveSettings();
        }
    }

    private void ExtensionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string extension, IsChecked: bool isChecked })
        {
            return;
        }

        if (isChecked)
        {
            if (!TextFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                TextFileExtensions.Add(extension);
            }
        }
        else
        {
            TextFileExtensions.Remove(extension);
        }

        SaveSettings();
    }

    private void HtmlSearchModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HtmlSearchModeComboBox.SelectedItem is not ComboBoxItem { Tag: string selectedModeText }
            || !Enum.TryParse(selectedModeText, out HtmlSearchMode selectedMode))
        {
            return;
        }

        if (AppState.HtmlSearchMode == selectedMode)
        {
            return;
        }

        AppState.HtmlSearchMode = selectedMode;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            AppState.SaveSearchFolders();
            UpdateStatus();
        }
        catch (System.IO.IOException ex)
        {
            StatusTextBlock.Text = $"Kunne ikke lagre innstillingene: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusTextBlock.Text = $"Mangler tilgang til å lagre innstillingene: {ex.Message}";
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
            : $"{SearchFolders.Count} mappe(r) valgt. Filtyper: {TextFileExtensions.Count}. HTML-søk: {GetHtmlSearchModeDisplayText(AppState.HtmlSearchMode)}.";
    }

    private void SetHtmlSearchModeSelection()
    {
        string selectedTag = AppState.HtmlSearchMode.ToString();
        ComboBoxItem? selectedItem = HtmlSearchModeComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, selectedTag, StringComparison.Ordinal));
        if (selectedItem is not null)
        {
            HtmlSearchModeComboBox.SelectedItem = selectedItem;
        }
    }

    private static string GetHtmlSearchModeDisplayText(HtmlSearchMode mode)
    {
        return mode == HtmlSearchMode.InnerText
            ? "innerText"
            : "HTML-kilde";
    }

    private void BuildExtensionCheckBoxes()
    {
        ExtensionCheckBoxPanel.Children.Clear();

        foreach (string extension in AppState.AvailableTextFileExtensions)
        {
            CheckBox checkBox = new()
            {
                Content = extension,
                Tag = extension,
                IsChecked = TextFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            };

            AutomationProperties.SetAutomationId(checkBox, $"TextFileExtension_{extension[1..]}");
            AutomationProperties.SetName(checkBox, $"Filtype {extension}");

            checkBox.Checked += ExtensionCheckBox_Changed;
            checkBox.Unchecked += ExtensionCheckBox_Changed;

            ExtensionCheckBoxPanel.Children.Add(checkBox);
        }
    }
}
