using PdfPixel.Demo.Wpf.ViewModels;
using PdfPixel.PdfPanel.Mvvm;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PdfPixel.Demo.Wpf.Views;

/// <summary>
/// Interaction logic for DocumentTabView.xaml.
/// </summary>
public partial class DocumentTabView : UserControl
{
    public DocumentTabView()
    {
        InitializeComponent();
        DataContextChanged += DocumentTabView_DataContextChanged;
    }

    private void DocumentTabView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DocumentTabViewModel oldViewModel)
        {
            oldViewModel.LogMessages.CollectionChanged -= LogMessages_CollectionChanged;
        }

        if (e.NewValue is DocumentTabViewModel newViewModel)
        {
            newViewModel.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
        }
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
            item.IsVisible = true;
        }
    }

    private void ThumbnailItem_Unloaded(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfThumbnailViewModel item)
        {
            item.IsVisible = false;
        }
    }

    private void ThumbnailItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PdfThumbnailViewModel item)
        {
            pdfPanel.Thumbnails?.Activate(item);
        }
    }

    private void LogMessages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogDataGrid.Items.Count > 0)
            {
                var lastItem = LogDataGrid.Items[LogDataGrid.Items.Count - 1];
                LogDataGrid.ScrollIntoView(lastItem);
            }
        });
    }
}
