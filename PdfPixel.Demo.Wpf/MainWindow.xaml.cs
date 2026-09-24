using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        LocationChanged += MainWindow_PlacementChanged;
        SizeChanged += MainWindow_PlacementChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowsViewModel;
        viewModel?.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
    }

    private void MainWindow_PlacementChanged(object sender, EventArgs e)
    {
        // A popup keeps its screen position when its window moves; changing the offset makes it follow the text box.
        SearchPopup.HorizontalOffset += 1;
        SearchPopup.HorizontalOffset -= 1;
    }

    private void SearchResultItem_MouseEnter(object sender, MouseEventArgs e) => ((ListBoxItem)sender).IsSelected = true;

    private void LogMessages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && LogDataGrid.Items.Count > 0)
            {
                var lastItem = LogDataGrid.Items[LogDataGrid.Items.Count - 1];
                LogDataGrid.ScrollIntoView(lastItem);
            }
        });
    }
}
