using Microsoft.UI.Xaml.Controls;

namespace TextSearcher;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(SearchPage));
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        Type pageType = tag == "Admin" ? typeof(AdminPage) : typeof(SearchPage);
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
