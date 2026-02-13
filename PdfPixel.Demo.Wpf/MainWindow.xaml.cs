using System;
using System.Windows;

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
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowsViewModel;
        viewModel?.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
    }

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
