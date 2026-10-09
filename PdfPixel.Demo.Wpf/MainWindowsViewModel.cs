using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfPixel.Encryption.Model;
using PdfPixel.Fonts.Management;
using PdfPixel.Skia.Fonts;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Wpf;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PdfPixel.Models;
using System.Linq;

namespace PdfPixel.Demo.Wpf;

public class PdfFileLocation
{
    public PdfFileLocation(string filePath)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileNameWithoutExtension(FilePath);
    }

    public string FilePath { get; }

    public string FileName { get; }
}

public class MainWindowsViewModel : ObservableObject
{
    private readonly SkiaFontSubstitutor _fontSubstitutor;
    private readonly PdfDocumentReader _reader;
    private readonly ObservableLoggerFactory _loggerFactory;
    private readonly object _logMessagesLock = new object();

    private int _pageNumber;
    private PdfPanelAutoScaleMode _autoScaleMode;
    private PdfPanelPageCollection _pages;
    private IPdfDocument _document;
    private PdfFileLocation _selectedPdfFile;
    private bool _hasFiles;
    private string _searchQuery;
    private PdfPanelSearchMatch? _currentSearchResult;
    private bool _searchMatchCase;
    private bool _searchWholeWord;
    private bool _searchMatchDiacritics;

    public MainWindowsViewModel()
    {
        LogMessages = new ObservableCollection<LogMessage>();
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(LogMessages, _logMessagesLock);
        _loggerFactory = new ObservableLoggerFactory(LogMessages, _logMessagesLock);
        _fontSubstitutor = new SkiaFontSubstitutor(_loggerFactory);
        _reader = new PdfDocumentReader(_loggerFactory, _fontSubstitutor);

        PanelInterface = new WpfPdfPanelInterface();
        PanelInterface.OnAfterDraw = OnAfterDraw;
        RotatePageCommand = new RelayCommand(RotatePage);
        RotateAllPagesCommand = new RelayCommand(RotateAllPages);
        ZoomInCommand = new RelayCommand(() => PanelInterface.ZoomIn());
        ZoomOutCommand = new RelayCommand(() => PanelInterface.ZoomOut());
        NextSearchResultCommand = new RelayCommand(() => PanelInterface.NextSearchResult());
        PreviousSearchResultCommand = new RelayCommand(() => PanelInterface.PreviousSearchResult());

        LoadPdfFiles();
        ToggleAutoScaleCommand = new RelayCommand(ToggleAutoScale);
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToHeight;

        OpenFileCommand = new RelayCommand(OpenFile);
    }

    public ObservableCollection<LogMessage> LogMessages { get; }

    public int PageNumber
    {
        get => _pageNumber;
        set => SetProperty(ref _pageNumber, value);
    }

    public PdfPanelAutoScaleMode AutoScaleMode
    {
        get => _autoScaleMode;
        set
        {
            SetProperty(ref _autoScaleMode, value);
        }
    }

    public ICommand RotatePageCommand { get; }

    public ICommand RotateAllPagesCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand NextSearchResultCommand { get; }

    public ICommand PreviousSearchResultCommand { get; }

    public ICommand ToggleAutoScaleCommand { get; }

    public ICommand OpenFileCommand { get; }

    public WpfPdfPanelInterface PanelInterface { get; }

    public ObservableCollection<PdfFileLocation> PdfFiles { get; } = new ObservableCollection<PdfFileLocation>();

    public PdfFileLocation SelectedPdfFile
    {
        get => _selectedPdfFile;
        set
        {
            if (SetProperty(ref _selectedPdfFile, value))
            {
                LoadSelectedPdf();
            }
        }
    }

    public bool HasFiles
    {
        get => _hasFiles;
        set => _hasFiles = value;
    }

    public PdfPanelPageCollection Pages
    {
        get => _pages;
        set => SetProperty(ref _pages, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                OnPropertyChanged(nameof(ExtractText));
            }
        }
    }

    public bool ExtractText => !string.IsNullOrEmpty(SearchQuery);

    public PdfPanelSearchMatch? CurrentSearchResult
    {
        get => _currentSearchResult;
        set => SetProperty(ref _currentSearchResult, value);
    }

    public bool SearchMatchCase
    {
        get => _searchMatchCase;
        set => SetProperty(ref _searchMatchCase, value);
    }

    public bool SearchWholeWord
    {
        get => _searchWholeWord;
        set => SetProperty(ref _searchWholeWord, value);
    }

    public bool SearchMatchDiacritics
    {
        get => _searchMatchDiacritics;
        set => SetProperty(ref _searchMatchDiacritics, value);
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

        var file = new PdfFileLocation(filePath);

        PdfFiles.Add(file);
        SelectedPdfFile = file;
    }

    private void LoadPdfFiles()
    {
        var pdfDirectory = "./Pdfs";

        if (Directory.Exists(pdfDirectory))
        {
            var files = Directory.GetFiles(pdfDirectory, "*.pdf")
                .OrderBy(x => System.IO.Path.GetFileName(x), new NaturalStringComparer());

            foreach (var file in files)
            {
                PdfFiles.Add(new PdfFileLocation(file));
            }

            if (PdfFiles.Count > 0)
            {
                SelectedPdfFile = PdfFiles[0];
            }

            HasFiles = PdfFiles.Count > 0;
        }
    }

    private void LoadSelectedPdf()
    {
        FileInfo fileInfo = new FileInfo(SelectedPdfFile.FilePath);

        if (!fileInfo.Exists)
        {
            return;
        }

        var currentPages = Pages;
        Pages = null;
        _document?.Dispose();
        currentPages?.Dispose();

        IPdfDocument document = ReadDocumentWithPasswordPrompt(fileInfo);
        if (document == null)
        {
            _document = null;
            return;
        }

        _document = document;
        Pages = PdfPanelPageCollection.FromDocument(_document, _loggerFactory);
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToHeight;
    }

    private IPdfDocument ReadDocumentWithPasswordPrompt(FileInfo fileInfo)
    {
        Stream fileStream = OpenFileStream(fileInfo);

        try
        {
            return _reader.Read(fileStream, OnCredentialRequested);
        }
        catch (PdfAuthenticationException)
        {
            fileStream.Dispose();
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

    private static Stream OpenFileStream(FileInfo fileInfo)
    {
        if (fileInfo.Length < 50_000_000)
        {
            var fileBytes = File.ReadAllBytes(fileInfo.FullName);
            return new MemoryStream(fileBytes);
        }

        return File.OpenRead(fileInfo.FullName);
    }
}
