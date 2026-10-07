using System;
using System.Windows;
using System.Windows.Input;

namespace PdfPixel.PdfPanel.Wpf;

public partial class WpfPdfPanel
{
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
        e.CanExecute = _context?.Text.SelectedText.Length > 0;
        e.Handled = true;
    }

    private void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string text = _context?.Text.SelectedText ?? string.Empty;

        if (text.Length > 0)
        {
            Clipboard.SetText(text);
        }

        e.Handled = true;
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

