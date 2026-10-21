using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PdfPixel.Demo.Wpf.Handlers;
using PdfPixel.Demo.Wpf.Logging;
using PdfPixel.Demo.Wpf.Utilities;
using PdfPixel.Demo.Wpf.Views;
using PdfPixel.Encryption;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Wpf;
using PdfPixel.Skia.Fonts;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfPixel.Demo.Wpf.ViewModels;

/// <summary>
/// One document tab: the opened document, its view state and its log.
/// </summary>
public class DocumentTabViewModel : ObservableObject, IDisposable
{
    private const string EmptyTabTitle = "New tab";

    private readonly SkiaFontSubstitutor _fontSubstitutor;
    private readonly PdfDocumentReader _reader;
    private readonly ObservableLoggerFactory _loggerFactory;
    private readonly DocumentActionHandler _actionHandler;
    private readonly object _logMessagesLock = new();

    private DocumentTabRequest _request;
    private int _pageNumber;
    private PdfPanelAutoScaleMode _autoScaleMode;
    private PdfPanelPageCollection _pages;
    private IPdfDocument _document;
    private PdfFileLocation _selectedPdfFile;
    private string _title = EmptyTabTitle;
    private bool _isSelected;
    private string _searchQuery;
    private PdfPanelSearchMatch? _currentSearchResult;
    private bool _isSearchOpen;
    private bool _isLogsOpen;
    private DocumentSidePanel _sidePanel;
    private GridLength _sidePanelWidth = new(0);
    private GridLength _openSidePanelWidth = new(240);
    private GridLength _logsHeight = new(0);
    private GridLength _openLogsHeight = new(160);

    /// <summary>
    /// Initializes an empty tab that opens documents in new tabs through <paramref name="openTab"/>.
    /// </summary>
    public DocumentTabViewModel(Func<DocumentTabRequest, DocumentTabViewModel> openTab)
    {
        LogMessages = [];
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(LogMessages, _logMessagesLock);
        _loggerFactory = new ObservableLoggerFactory(LogMessages, _logMessagesLock);
        _actionHandler = new DocumentActionHandler(this, openTab, _loggerFactory.CreateLogger<DocumentActionHandler>());
        _fontSubstitutor = new SkiaFontSubstitutor(_loggerFactory);
        _reader = new PdfDocumentReader(_loggerFactory, _fontSubstitutor);

        PanelInterface = new PdfPanelInterface();
        PanelInterface.OnAfterDraw = OnAfterDraw;
        RotatePageCommand = new RelayCommand(RotatePage);
        RotateAllPagesCommand = new RelayCommand(RotateAllPages);
        ZoomInCommand = new RelayCommand(PanelInterface.ZoomIn);
        ZoomOutCommand = new RelayCommand(PanelInterface.ZoomOut);
        NextSearchResultCommand = new RelayCommand(PanelInterface.NextSearchResult);
        PreviousSearchResultCommand = new RelayCommand(PanelInterface.PreviousSearchResult);
        ToggleAutoScaleCommand = new RelayCommand(ToggleAutoScale);
        OpenFileCommand = new RelayCommand(OpenFile);
        CloseSearchCommand = new RelayCommand(CloseSearch);
        ActionRequestedCommand = new RelayCommand<WpfPdfPanelActionEventArgs>(_actionHandler.PerformAction);
        OpenAttachmentCommand = new RelayCommand<PdfAttachmentViewModel>(_actionHandler.OpenAttachment);

        LoadPdfFiles();
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToHeight;
    }

    public ObservableCollection<LogMessage> LogMessages { get; }

    public ICommand RotatePageCommand { get; }

    public ICommand RotateAllPagesCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand NextSearchResultCommand { get; }

    public ICommand PreviousSearchResultCommand { get; }

    public ICommand ToggleAutoScaleCommand { get; }

    public ICommand OpenFileCommand { get; }

    public ICommand CloseSearchCommand { get; }

    /// <summary>
    /// Performs an action the panel asks the host to perform.
    /// </summary>
    public ICommand ActionRequestedCommand { get; }

    /// <summary>
    /// Opens a PDF attachment in a new tab, or saves any other attachment to a file.
    /// </summary>
    public ICommand OpenAttachmentCommand { get; }

    public PdfPanelInterface PanelInterface { get; }

    public PdfPanelConfiguration PanelConfiguration { get; } = new() { PageCornerRadius = 4 };

    public ObservableCollection<PdfFileLocation> PdfFiles { get; } = [];

    public bool HasFiles => PdfFiles.Count > 0;

    /// <summary>
    /// Document title, the file name when the document has none, or "New tab" when no document is open.
    /// </summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public bool HasDocument => _document != null;

    /// <summary>
    /// Request the open document was loaded from, or null when no document is open.
    /// </summary>
    internal DocumentTabRequest Request => _request;

    /// <summary>
    /// The open document, or null when none is open.
    /// </summary>
    internal IPdfDocument Document => _document;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public PdfFileLocation SelectedPdfFile
    {
        get => _selectedPdfFile;
        set
        {
            if (SetProperty(ref _selectedPdfFile, value) && value != null)
            {
                Load(DocumentTabRequest.FromFile(value.FilePath));
            }
        }
    }

    public int PageNumber
    {
        get => _pageNumber;
        set => SetProperty(ref _pageNumber, value);
    }

    public PdfPanelAutoScaleMode AutoScaleMode
    {
        get => _autoScaleMode;
        set => SetProperty(ref _autoScaleMode, value);
    }

    public PdfPanelPageCollection Pages
    {
        get => _pages;
        private set => SetProperty(ref _pages, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                PanelConfiguration.ExtractAllText = !string.IsNullOrEmpty(value);
            }
        }
    }

    public PdfPanelSearchMatch? CurrentSearchResult
    {
        get => _currentSearchResult;
        set => SetProperty(ref _currentSearchResult, value);
    }

    /// <summary>
    /// Whether the search bar is shown; hiding it clears the search query.
    /// </summary>
    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set
        {
            if (SetProperty(ref _isSearchOpen, value) && !value)
            {
                SearchQuery = null;
            }
        }
    }

    public bool IsLogsOpen
    {
        get => _isLogsOpen;
        set
        {
            if (!SetProperty(ref _isLogsOpen, value))
            {
                return;
            }

            if (value)
            {
                LogsHeight = _openLogsHeight;
            }
            else
            {
                _openLogsHeight = LogsHeight;
                LogsHeight = new GridLength(0);
            }
        }
    }

    public GridLength LogsHeight
    {
        get => _logsHeight;
        set => SetProperty(ref _logsHeight, value);
    }

    /// <summary>
    /// Block shown in the side panel; <see cref="DocumentSidePanel.None"/> collapses it.
    /// </summary>
    public DocumentSidePanel SidePanel
    {
        get => _sidePanel;
        set
        {
            DocumentSidePanel previous = _sidePanel;

            if (!SetProperty(ref _sidePanel, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSidePanelOpen));

            if (previous == DocumentSidePanel.None)
            {
                SidePanelWidth = _openSidePanelWidth;
            }
            else if (value == DocumentSidePanel.None)
            {
                _openSidePanelWidth = SidePanelWidth;
                SidePanelWidth = new GridLength(0);
            }
        }
    }

    public bool IsSidePanelOpen => _sidePanel != DocumentSidePanel.None;

    public GridLength SidePanelWidth
    {
        get => _sidePanelWidth;
        set => SetProperty(ref _sidePanelWidth, value);
    }

    /// <summary>
    /// Opens the document of <paramref name="request"/> in this tab, replacing the current one.
    /// </summary>
    public void Load(DocumentTabRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        CloseDocument();

        IPdfDocument document = ReadDocumentWithPasswordPrompt(request.Data);
        if (document == null)
        {
            Title = EmptyTabTitle;
            OnPropertyChanged(nameof(HasDocument));
            return;
        }

        _document = document;
        _request = request;
        Title = GetTitle(document, request.FileName);
        SidePanel = DocumentSidePanel.None;
        Pages = PdfPanelPageCollection.FromDocument(_document, _loggerFactory, SynchronizationContext.Current);
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToHeight;
        OnPropertyChanged(nameof(HasDocument));

        if (request.Destination != null)
        {
            PanelInterface.ScrollToDestination(request.Destination);
        }
    }

    private static string GetTitle(IPdfDocument document, string fileName)
    {
        PdfString? title = document.Information?.Title;
        if (title != null)
        {
            string decodedTitle = title.Value.DecodePdfString();
            if (!string.IsNullOrWhiteSpace(decodedTitle))
            {
                return decodedTitle;
            }
        }

        return fileName;
    }

    private void OnAfterDraw(SKCanvas canvas, PdfPanelFrame frame)
    {
        SKColor defaultColor = SKColor.Parse("#21232B").WithAlpha(128);
        SKColor accentColor = SKColor.Parse("#4695EB").WithAlpha(128);

        using var defaultPaint = new SKPaint { Color = defaultColor, Style = SKPaintStyle.Fill };
        using var accentPaint = new SKPaint { Color = accentColor, Style = SKPaintStyle.Fill };

        var layerPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(128),
            Style = SKPaintStyle.Fill,
        };

        canvas.Translate(frame.PanelSize.Width - 58, frame.PanelSize.Height - 48);
        canvas.Scale(0.5f, 0.5f);

        const float cellSize = 18;
        const float padding = 4;

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                float x = i * (cellSize + padding);
                float y = j * (cellSize + padding);

                if (i == 1 && j == 1)
                {
                    canvas.DrawRect(x, y, cellSize, cellSize, accentPaint);
                }
                else
                {
                    canvas.DrawRect(x, y, cellSize, cellSize, defaultPaint);
                }
            }
        }
    }

    private void RotatePage()
    {
        if (Pages == null || PageNumber < 1 || PageNumber > Pages.Count)
        {
            return;
        }

        var oldPage = PageNumber;

        Pages[PageNumber - 1].UserRotation += 90;
        PanelInterface.RequestRedraw();

        PageNumber = oldPage;
    }

    private void RotateAllPages()
    {
        if (Pages == null)
        {
            return;
        }

        var oldPage = PageNumber;

        foreach (var page in Pages)
        {
            page.UserRotation += 90;
        }

        PanelInterface.RequestRedraw();

        PageNumber = oldPage;
    }

    private void ToggleAutoScale()
    {
        if (AutoScaleMode == PdfPanelAutoScaleMode.ScaleToWidth)
        {
            AutoScaleMode = PdfPanelAutoScaleMode.ScaleToHeight;
        }
        else
        {
            AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        }
    }

    private void CloseSearch() => IsSearchOpen = false;

    private void OpenFile()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Open PDF File"
        };

        bool? result = openFileDialog.ShowDialog();
        if (result != true)
        {
            return;
        }

        string filePath = openFileDialog.FileName;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        Load(DocumentTabRequest.FromFile(filePath));
    }

    private void LoadPdfFiles()
    {
        var pdfDirectory = "./Pdfs";

        if (!Directory.Exists(pdfDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(pdfDirectory, "*.pdf")
            .OrderBy(x => System.IO.Path.GetFileName(x), new NaturalStringComparer());

        foreach (var file in files)
        {
            PdfFiles.Add(new PdfFileLocation(file));
        }
    }

    /// <summary>
    /// Reads a document from <paramref name="data"/>, prompting for its password; null when no password is accepted.
    /// </summary>
    internal IPdfDocument ReadDocumentWithPasswordPrompt(byte[] data)
    {
        MemoryStream stream = new(data);

        try
        {
            return _reader.Read(stream, OnCredentialRequested);
        }
        catch (PdfAuthenticationException)
        {
            stream.Dispose();
            return null;
        }
    }

    private static PdfCredential OnCredentialRequested(PdfCredentialRequest request)
    {
        string errorMessage = null;
        if (request.Reason == PdfCredentialRequestReason.CredentialRejected)
        {
            errorMessage = "Incorrect password. Please try again.";
        }

        string password = PasswordPromptWindow.TryPromptForPassword(Application.Current.MainWindow, errorMessage);
        if (password == null)
        {
            return null;
        }

        return new PdfPasswordCredential(password);
    }

    private void CloseDocument()
    {
        var currentPages = Pages;
        Pages = null;
        _document?.Dispose();
        _document = null;
        currentPages?.Dispose();
    }

    public void Dispose() => CloseDocument();
}
