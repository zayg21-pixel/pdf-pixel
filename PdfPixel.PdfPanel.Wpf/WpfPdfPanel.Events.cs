using System.Windows;
using System.Windows.Input;

namespace PdfPixel.PdfPanel.Wpf;

public partial class WpfPdfPanel
{
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }

    private void OnPreNotifyInput(object sender, NotifyInputEventArgs e)
    {
        if (!IsMouseOver)
        {
            return;
        }

        KeyEventArgs keyArgs = e.StagingItem.Input as KeyEventArgs;

        if (keyArgs == null || keyArgs.RoutedEvent != Keyboard.KeyDownEvent)
        {
            return;
        }

        if (keyArgs.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            string text = _renderer?.TextLayer.SelectedText ?? string.Empty;
            if (text.Length > 0)
            {
                Clipboard.SetText(text);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (Pages != null && _context != null)
        {
            InvalidateVisual();
        }
    }
}

