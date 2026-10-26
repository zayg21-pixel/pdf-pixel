using Microsoft.Extensions.Logging;
using PdfPixel.Actions;
using PdfPixel.Objects;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.Skia.Fonts;
using System.ComponentModel;

namespace PdfPixel.Demo.Maui;

public partial class MainPage : ContentPage
{
    private static readonly PickOptions PdfPickOptions = CreatePdfPickOptions();

    private readonly ILoggerFactory _loggerFactory;
    private readonly PdfDocumentReader _reader;
    private PdfPanelDocument? _document;

    public MainPage(ILoggerFactory loggerFactory)
    {
        InitializeComponent();

        _loggerFactory = loggerFactory;
        _reader = new PdfDocumentReader(loggerFactory, new SkiaFontSubstitutor(loggerFactory));

        PdfPanelConfiguration configuration = new()
        {
            PageCornerRadius = 4
        };

        PdfPanel.Configuration = configuration;
        PdfPanel.PropertyChanged += OnPanelPropertyChanged;
        UpdateState();
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        FileResult? file = await FilePicker.Default.PickAsync(PdfPickOptions);

        if (file == null)
        {
            return;
        }

        try
        {
            using Stream fileStream = await file.OpenReadAsync();
            using MemoryStream memoryStream = new();
            await fileStream.CopyToAsync(memoryStream);

            Open(memoryStream.ToArray());
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Cannot open document", exception.Message, "OK");
        }
    }

    private void Open(byte[] data)
    {
        IPdfDocument document = _reader.Read(new MemoryStream(data));

        CloseDocument();

        _document = PdfPanelDocument.FromDocument(document, _loggerFactory, SynchronizationContext.Current);
        PdfPanel.Document = _document;
        PdfPanel.AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        UpdateState();
    }

    private void CloseDocument()
    {
        PdfPanelDocument? currentDocument = _document;
        _document = null;
        PdfPanel.Document = null;
        currentDocument?.Document.Dispose();
        currentDocument?.Dispose();
    }

    private void OnPreviousPageClicked(object? sender, EventArgs e) => PdfPanel.CurrentPage--;

    private void OnNextPageClicked(object? sender, EventArgs e) => PdfPanel.CurrentPage++;

    private void OnZoomOutClicked(object? sender, EventArgs e) => PdfPanel.ZoomOut();

    private void OnZoomInClicked(object? sender, EventArgs e) => PdfPanel.ZoomIn();

    private void OnFitWidthClicked(object? sender, EventArgs e) => PdfPanel.AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;

    private async void OnActionRequested(object? sender, PdfPanelActionEventArgs e)
    {
        if (e.Action is not PdfUriAction uriAction || uriAction.Uri == null)
        {
            return;
        }

        string uri = uriAction.Uri.Value.DecodePdfString();

        if (string.IsNullOrEmpty(uri) || !await DisplayAlertAsync("Open Link", uri, "Open", "Cancel"))
        {
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Cannot open link", exception.Message, "OK");
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfPanel.CurrentPage) || e.PropertyName == nameof(PdfPanel.PageLabel))
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        EmptyState.IsVisible = _document == null;
        PageLabel.Text = (_document == null) ? string.Empty : $"{PdfPanel.PageLabel} ({PdfPanel.CurrentPage} / {_document.Pages.Count})";
    }

    private static PickOptions CreatePdfPickOptions()
    {
        Dictionary<DevicePlatform, IEnumerable<string>> fileTypes = new()
        {
            [DevicePlatform.Android] = ["application/pdf"]
        };

        return new PickOptions
        {
            PickerTitle = "Open PDF",
            FileTypes = new FilePickerFileType(fileTypes)
        };
    }
}
