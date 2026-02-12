using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Wpf;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace PdfPixel.Demo.Wpf;

public class PdfFileLocation
{
    public PdfFileLocation(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileNameWithoutExtension(FilePath);
    }

    public string FilePath { get; }

    public string FileName { get; }
}

public class MainWindowsViewModel : ObservableObject
{
    private static readonly ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider("Times New Roman");
    private readonly PdfDocumentReader _reader;

    private int _pageNumber;
    private PdfPanelAutoScaleMode _autoScaleMode;
    private PdfPanelPageCollection _pages;
    private PdfFileLocation _selectedPdfFile;
    private bool _hasFiles;

    public MainWindowsViewModel()
    {
        _reader = new PdfDocumentReader(new LoggerFactory(), FontProvider);

        PanelInterface = new WpfPdfPanelInterface();
        PanelInterface.OnAfterDraw = OnAfterDraw;
        RotatePageCommand = new RelayCommand(RotatePage);
        RotateAllPagesCommand = new RelayCommand(RotateAllPages);
        ZoomInCommand = new RelayCommand(() => PanelInterface.ZoomIn());
        ZoomOutCommand = new RelayCommand(() => PanelInterface.ZoomOut());

        LoadPdfFiles();
        ToggleAutoScaleCommand = new RelayCommand(ToggleAutoScale);
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;

        OpenFileCommand = new RelayCommand(OpenFile);
    }

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

    private void OnAfterDraw(SKCanvas canvas, DrawingRequest request)
    {
        SKColor defaultColor = SKColor.Parse("#21232B");
        SKColor accentColor = SKColor.Parse("#4695EB");

        using var defaultPaint = new SKPaint { Color = defaultColor, Style = SKPaintStyle.Fill };
        using var accentPaint = new SKPaint { Color = accentColor, Style = SKPaintStyle.Fill };

        var layerPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(128),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.SaveLayer(layerPaint);

        canvas.Translate(request.CanvasSize.Width - 58, request.CanvasSize.Height - 48);
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

        canvas.Restore();
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
            var files = Directory.GetFiles(pdfDirectory, "*.pdf");

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
        if (!File.Exists(SelectedPdfFile.FilePath))
        {
            return;
        }

        var currentPages = Pages;
        Pages = null;
        currentPages?.Dispose();

        var fileStream = File.OpenRead(SelectedPdfFile.FilePath);
        var pdfDocument = _reader.Read(fileStream);
        Pages = PdfPanelPageCollection.FromDocument(pdfDocument);
        AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
    }
}
