using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.PdfPanel.Content;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

public partial class WpfPdfPanel
{
    /// <summary>
    /// Clipboard format name applications read PNG data under.
    /// </summary>
    private const string PngClipboardFormat = "PNG";

    /// <inheritdoc />
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        base.OnMouseDown(e);

        if (Focus())
        {
            e.Handled = true;
        }

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }

    private void OnCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _context != null
            && (_context.Content.SelectedImage != null || _context.Text.SelectedText.Length > 0);
        e.Handled = true;
    }

    private void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        PdfPanelImageContentItem? selectedImage = _context?.Content.SelectedImage;

        if (selectedImage != null)
        {
            CopyImage(selectedImage.Image);
        }
        else
        {
            string text = _context?.Text.SelectedText ?? string.Empty;

            if (text.Length > 0)
            {
                Clipboard.SetText(text);
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Puts <paramref name="image"/> on the clipboard as PNG data and as a bitmap.
    /// </summary>
    private static void CopyImage(PdfImage image)
    {
        PdfCancellationExecutionObserver observer = new(CancellationToken.None);
        byte[] png = image.DecodePng(PdfImageDecodingParameters.Default, observer);

        BitmapFrame bitmap = BitmapFrame.Create(new MemoryStream(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        DataObject data = new();
        data.SetData(PngClipboardFormat, new MemoryStream(png));
        data.SetImage(bitmap);

        Clipboard.SetDataObject(data, copy: true);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }
}

