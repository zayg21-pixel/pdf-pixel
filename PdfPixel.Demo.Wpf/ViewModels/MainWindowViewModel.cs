using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PdfPixel.Demo.Wpf.ViewModels;

/// <summary>
/// Main window: the open document tabs and the selected one.
/// </summary>
public class MainWindowViewModel : ObservableObject
{
    private DocumentTabViewModel _selectedTab;

    public MainWindowViewModel()
    {
        NewTabCommand = new RelayCommand(NewTab);
        CloseTabCommand = new RelayCommand<DocumentTabViewModel>(CloseTab);
        NewTab();
    }

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

    public ICommand NewTabCommand { get; }

    public ICommand CloseTabCommand { get; }

    public DocumentTabViewModel SelectedTab
    {
        get => _selectedTab;
        set
        {
            DocumentTabViewModel previous = _selectedTab;

            if (!SetProperty(ref _selectedTab, value))
            {
                return;
            }

            if (previous != null)
            {
                previous.IsSelected = false;
            }

            if (value != null)
            {
                value.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// Opens the document of <paramref name="request"/> in a new selected tab.
    /// </summary>
    public DocumentTabViewModel OpenTab(DocumentTabRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        DocumentTabViewModel tab = AddTab();
        tab.Load(request);

        return tab;
    }

    private void NewTab() => AddTab();

    private DocumentTabViewModel AddTab()
    {
        DocumentTabViewModel tab = new();
        Tabs.Add(tab);
        SelectedTab = tab;

        return tab;
    }

    private void CloseTab(DocumentTabViewModel tab)
    {
        int index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Tabs.RemoveAt(index);
        tab.Dispose();

        if (Tabs.Count == 0)
        {
            AddTab();
            return;
        }

        if (tab == SelectedTab)
        {
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        }
    }
}
