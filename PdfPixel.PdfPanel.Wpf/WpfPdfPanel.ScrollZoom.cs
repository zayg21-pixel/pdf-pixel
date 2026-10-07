using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Contains implementation of the IScrollInfo interface and methods for scrolling and zooming.
/// </summary>
public partial class WpfPdfPanel : IScrollInfo
{
    private const int WM_MOUSEHWHEEL = 0x020E;

    /// <inheritdoc />
    public bool CanHorizontallyScroll { get; set; } = true;

    /// <inheritdoc />
    public bool CanVerticallyScroll { get; set; } = true;

    /// <inheritdoc />
    public double ExtentHeight { get; set; }

    /// <inheritdoc />
    public double ExtentWidth { get; set; }

    /// <inheritdoc />
    public double HorizontalOffset { get; set; }

    /// <inheritdoc />
    public ScrollViewer? ScrollOwner { get; set; }

    /// <inheritdoc />
    public double VerticalOffset { get; set; }

    /// <inheritdoc />
    public double ViewportHeight { get; set; }

    /// <inheritdoc />
    public double ViewportWidth { get; set; }

    /// <summary>
    /// Scrolls to the page with the specified 1-based number.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    public void ScrollToPage(int pageNumber) => _context?.ScrollToPage(pageNumber);

    /// <inheritdoc />
    public void LineDown() => SetVerticalOffset(VerticalOffset + ScrollTick);

    /// <inheritdoc />
    public void LineUp() => SetVerticalOffset(VerticalOffset - ScrollTick);

    /// <inheritdoc />
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - ScrollTick);

    /// <inheritdoc />
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + ScrollTick);

    /// <inheritdoc />
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

#pragma warning disable RCS1231 // Make parameter ref read-only
    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
#pragma warning restore RCS1231 // Make parameter ref read-only
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            OnMouseTilt(unchecked((short)((int)wParam >> 16)));
            return 1;
        }

        return 0;
    }

    private void OnMouseTilt(int tilt)
    {
        if (tilt < 0)
        {
            MouseWheelLeft();
        }
        else if (tilt > 0)
        {
            MouseWheelRight();
        }
    }

    /// <inheritdoc />
    public void MouseWheelDown()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            ZoomOut();
        }
        else
        {
            SetVerticalOffset(VerticalOffset + ScrollTick);
        }
    }

    /// <inheritdoc />
    public void MouseWheelUp()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            ZoomIn();
        }
        else
        {
            SetVerticalOffset(VerticalOffset - ScrollTick);
        }
    }

    /// <summary>
    /// Increases the zoom level by <see cref="ZoomStep"/>.
    /// </summary>
    public void ZoomIn()
    {
        if (_context == null)
        {
            return;
        }

        PdfPoint center = GetZoomCenter(_context);

        _context.ZoomIn(center.X, center.Y);
        InvalidateVisual();
    }

    /// <summary>
    /// Decreases the zoom level by <see cref="ZoomStep"/>.
    /// </summary>
    public void ZoomOut()
    {
        if (_context == null)
        {
            return;
        }

        PdfPoint center = GetZoomCenter(_context);

        _context.ZoomOut(center.X, center.Y);
        InvalidateVisual();
    }

    private void OnScaleChanged()
    {
        if (_context == null)
        {
            return;
        }

        PdfPoint center = GetZoomCenter(_context);

        _context.Zoom((float)Scale, center.X, center.Y);
        InvalidateVisual();
    }

    private PdfPoint GetZoomCenter(PdfPanelContext context)
    {
        if (IsMouseOver)
        {
            return GetPanelPosition(Mouse.GetPosition(this));
        }

        return new PdfPoint(context.PanelWidth / 2, context.PanelHeight / 2);
    }

    /// <inheritdoc />
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - (ScrollTick * Scale));

    /// <inheritdoc />
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + (ScrollTick * Scale));

    /// <inheritdoc />
    public void PageDown()
    {
        if (_context != null)
        {
            SetVerticalOffset(VerticalOffset + _context.PanelHeight);
        }
    }

    /// <inheritdoc />
    public void PageUp()
    {
        if (_context != null)
        {
            SetVerticalOffset(VerticalOffset - _context.PanelHeight);
        }
    }

    /// <inheritdoc />
    public void PageLeft()
    {
        if (_context != null)
        {
            SetHorizontalOffset(HorizontalOffset - _context.PanelWidth);
        }
    }

    /// <inheritdoc />
    public void PageRight()
    {
        if (_context != null)
        {
            SetHorizontalOffset(HorizontalOffset + _context.PanelWidth);
        }
    }

    /// <summary>
    /// Sets the vertical offset.
    /// </summary>
    /// <param name="offset">The target vertical offset.</param>
    public void SetVerticalOffset(double offset)
    {
        if (_context == null)
        {
            return;
        }

        _context.VerticalOffset = (float)offset;
        InvalidateVisual();
    }

    /// <summary>
    /// Sets the horizontal offset.
    /// </summary>
    /// <param name="offset">The target horizontal offset.</param>
    public void SetHorizontalOffset(double offset)
    {
        if (_context == null)
        {
            return;
        }

        _context.HorizontalOffset = (float)offset;
        InvalidateVisual();
    }
}
