using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Management;
using PdfReader.Wpf.PdfPanel;
using System;
using System.IO;
using System.Windows;

namespace PdfReader.Wpf.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string[] pdfFiles;
        private readonly PdfDocumentReader reader;
        private static readonly ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider("Times New Roman");

        public MainWindow()
        {
            InitializeComponent();

            reader = new PdfDocumentReader(new LoggerFactory(), FontProvider);

            pdfFiles = Directory.GetFiles("./Pdfs", "*.pdf");
            FilesCombo.ItemsSource = pdfFiles;
            FilesCombo.SelectedIndex = 0;

            AutoScaleModeCombo.ItemsSource = Enum.GetValues(typeof(PdfPanelAutoScaleMode));
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            pdfPanel.ZoomIn();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            pdfPanel.ZoomOut();
        }

        private void RotateOnePage_Click(object sender, RoutedEventArgs e)
        {
            var current = pdfPanel.CurrentPage;
            var page = pdfPanel.Pages[current - 1];
            page.UserRotation = GetNextRotation(page.UserRotation);

            pdfPanel.InvalidateVisual();
        }

        private int GetNextRotation(int userRotation)
        {
            return (userRotation + 90) % 360;
        }

        private void FilesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var currentPages = pdfPanel.Pages;
            pdfPanel.Pages = null;
            currentPages?.Dispose();

            var fileStream = File.OpenRead(pdfFiles[FilesCombo.SelectedIndex]);
            reader.Read(fileStream);
            var pdfDocument = reader.Read(fileStream);
            pdfPanel.Pages = PdfViewerPageCollection.FromDocument(pdfDocument);
            pdfPanel.AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        }

        private void FitVisible_Click(object sender, RoutedEventArgs e)
        {
            pdfPanel.AutoScaleMode = PdfPanelAutoScaleMode.ScaleToVisible;
        }

        private void FitWidth_Click(object sender, RoutedEventArgs e)
        {
            pdfPanel.AutoScaleMode = PdfPanelAutoScaleMode.ScaleToWidth;
        }
    }
}