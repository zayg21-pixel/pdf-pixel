using PdfPixel.PdfPanel.Mvvm;
using System.Windows;
using System.Windows.Input;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    private GridLength _sidePanelWidth = new(220);
    private double _sidePanelMinWidth = 120;

    public MainWindow()
    {
        InitializeComponent();
        SidePanelToggleButton.Checked += SidePanelToggleButton_Checked;
        SidePanelToggleButton.Unchecked += SidePanelToggleButton_Unchecked;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowsViewModel;
        viewModel?.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
    }

    private void SidePanelToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        SidePanelColumn.MinWidth = _sidePanelMinWidth;
        SidePanelColumn.Width = _sidePanelWidth;
    }

    private void SidePanelToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _sidePanelWidth = SidePanelColumn.Width;
        _sidePanelMinWidth = SidePanelColumn.MinWidth;
        SidePanelColumn.MinWidth = 0;
        SidePanelColumn.Width = new GridLength(0);
    }

    private void OutlineItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfOutlineItemViewModel item)
        {
            pdfPanel.Outlines?.Activate(item);
        }
    }

    private void ThumbnailItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfThumbnailViewModel item)
        {
            item.IsRequested = true;
        }
    }

    private void ThumbnailItem_Unloaded(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfThumbnailViewModel item)
        {
            item.IsRequested = false;
        }
    }

    private void ThumbnailItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfThumbnailViewModel item)
        {
            pdfPanel.Thumbnails?.Activate(item);
        }
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
