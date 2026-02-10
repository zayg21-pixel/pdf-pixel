using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace PdfPixel.Wpf.Demo
{
    public class MainWindowsViewModel : ObservableObject
    {
        private static readonly ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider("Times New Roman");
        private readonly PdfDocumentReader reader;

        private int pageNumber;
        private PdfPanelAutoScaleMode autoScaleMode;
        private PdfPanelPageCollection pages;
        private string selectedPdfFile;

        public MainWindowsViewModel()
        {
            reader = new PdfDocumentReader(new LoggerFactory(), FontProvider);

            PanelInterface = new WpfPdfPanelInterface();
            RotatePageCommand = new RelayCommand(RotatePage);
            RotateAllPagesCommand = new RelayCommand(RotateAllPages);
            ZoomInCommand = new RelayCommand(() => PanelInterface.ZoomIn());
            ZoomOutCommand = new RelayCommand(() => PanelInterface.ZoomOut());

            LoadPdfFiles();
            AutoScaleModes = Enum.GetValues(typeof(PdfPanelAutoScaleMode)).Cast<PdfPanelAutoScaleMode>().ToList();
            AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        }

        public int PageNumber
        {
            get => pageNumber;
            set => SetProperty(ref pageNumber, value);
        }

        public PdfPanelAutoScaleMode AutoScaleMode
        {
            get => autoScaleMode;
            set => SetProperty(ref autoScaleMode, value);
        }

        public ICommand RotatePageCommand { get; }

        public ICommand RotateAllPagesCommand { get; }

        public ICommand ZoomInCommand { get; }

        public ICommand ZoomOutCommand { get; }

        public WpfPdfPanelInterface PanelInterface { get; }

        public List<string> PdfFiles { get; private set; }

        public List<PdfPanelAutoScaleMode> AutoScaleModes { get; }

        public string SelectedPdfFile
        {
            get => selectedPdfFile;
            set
            {
                if (SetProperty(ref selectedPdfFile, value))
                {
                    LoadSelectedPdf();
                }
            }
        }

        public PdfPanelPageCollection Pages
        {
            get => pages;
            set
            {
                SetProperty(ref pages, value);

                //if (value != null)
                //{
                //    pages.OnAfterDraw = AfterDrawDelegate;
                //}
            }
        }

        //private void AfterDrawDelegate(SKCanvas canvas, VisiblePageInfo[] visiblePages, double scale)
        //{
        //    canvas.Scale((float)scale, (float)scale);

        //    foreach (var visiblePage in visiblePages)
        //    {
        //        var canvasPosition = new System.Windows.Point(30, 30);
        //        var pagePosition = visiblePage.ToCanvasPosition(canvasPosition, 1);
        //        var devicePosition = new SKPoint((float)pagePosition.X, (float)pagePosition.Y);
        //        //canvas.DrawText($"Page {visiblePage.PageNumber}", devicePosition.X, devicePosition.Y);
        //    }
        //}


        private void RotatePage()
        {
            if (Pages == null || PageNumber < 1 || PageNumber > Pages.Count)
            {
                return;
            }

            Pages[PageNumber - 1].UserRotation += 90;
            PanelInterface.RequestRedraw();
        }

        private void RotateAllPages()
        {
            if (Pages == null)
            {
                return;
            }

            foreach (var page in Pages)
            {
                page.UserRotation += 90;
            }

            PanelInterface.RequestRedraw();
        }

        private void LoadPdfFiles()
        {
            var pdfDirectory = "./Pdfs";

            if (Directory.Exists(pdfDirectory))
            {
                PdfFiles = Directory.GetFiles(pdfDirectory, "*.pdf").ToList();

                if (PdfFiles.Count > 0)
                {
                    SelectedPdfFile = PdfFiles[0];
                }
            }
            else
            {
                PdfFiles = new List<string>();
            }
        }

        private void LoadSelectedPdf()
        {
            if (string.IsNullOrEmpty(SelectedPdfFile) || !File.Exists(SelectedPdfFile))
            {
                return;
            }

            var currentPages = Pages;
            Pages = null;
            currentPages?.Dispose();

            var fileStream = File.OpenRead(SelectedPdfFile);
            var pdfDocument = reader.Read(fileStream);
            Pages = PdfPanelPageCollection.FromDocument(pdfDocument);
            AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        }
    }
}
