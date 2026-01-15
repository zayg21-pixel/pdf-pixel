using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PdfReader.Fonts.Management;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.IO;
using System.Windows;

namespace PdfReader.Wpf.DirectXExperiments
{
    public partial class MainWindow : Window
    {
        private static readonly ILoggerFactory LoggerFactoryInstance = LoggerFactory.Create(builder => builder.AddConsole());
        private static readonly ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider();

        private PdfDocument _document;
        private int _currentPageNumber = 1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnOpenPdf(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Select a PDF file"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadPdf(dialog.FileName);
            }
        }

        private void LoadPdf(string fileName)
        {
            try
            {
                StatusTextBlock.Text = $"Loading {Path.GetFileName(fileName)}...";

                _document?.Dispose();

                var reader = new PdfDocumentReader(LoggerFactoryInstance, FontProvider);
                var fileStream = File.OpenRead(fileName);
                _document = reader.Read(fileStream);

                _currentPageNumber = 1;
                PageNumberTextBox.Text = "1";
                TotalPagesTextBlock.Text = $"/ {_document.Pages.Count}";

                PreviousButton.IsEnabled = false;
                NextButton.IsEnabled = _document.Pages.Count > 1;
                RenderButton.IsEnabled = true;

                StatusTextBlock.Text = $"Loaded {Path.GetFileName(fileName)} - {_document.Pages.Count} page(s)";

                RenderCurrentPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Error loading PDF";
            }
        }

        private void OnPreviousPage(object sender, RoutedEventArgs e)
        {
            if (_currentPageNumber > 1)
            {
                _currentPageNumber--;
                PageNumberTextBox.Text = _currentPageNumber.ToString();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        private void OnNextPage(object sender, RoutedEventArgs e)
        {
            if (_currentPageNumber < _document.Pages.Count)
            {
                _currentPageNumber++;
                PageNumberTextBox.Text = _currentPageNumber.ToString();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        private void OnRender(object sender, RoutedEventArgs e)
        {
            RenderCurrentPage();
        }

        private void UpdateNavigationButtons()
        {
            PreviousButton.IsEnabled = _currentPageNumber > 1;
            NextButton.IsEnabled = _currentPageNumber < _document.Pages.Count;
        }

        private void RenderCurrentPage()
        {
            if (_document == null)
            {
                return;
            }

            try
            {
                StatusTextBlock.Text = $"Rendering page {_currentPageNumber}...";

                var page = _document.Pages[_currentPageNumber - 1];
                var renderingBounds = page.CropBox;

                DirectXImage.Width = renderingBounds.Width;
                DirectXImage.Height = renderingBounds.Height;

                DirectXImage.RenderPdf(canvas =>
                {
                    canvas.Clear(SKColors.White);
                    canvas.ClipRect(new SKRect(0, 0, renderingBounds.Width, renderingBounds.Height));
                    page.Draw(canvas, new PdfRenderingParameters());
                });

                StatusTextBlock.Text = $"Page {_currentPageNumber} rendered using DirectX + D3DImage";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rendering page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Error rendering page";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _document?.Dispose();
        }
    }
}
