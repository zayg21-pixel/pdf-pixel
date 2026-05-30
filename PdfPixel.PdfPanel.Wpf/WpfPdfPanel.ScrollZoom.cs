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

    public bool CanHorizontallyScroll { get; set; } = true;

    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentHeight { get; set; }

    public double ExtentWidth { get; set; }

    public double HorizontalOffset { get; set; }

    public ScrollViewer ScrollOwner { get; set; }

    public double VerticalOffset { get; set; }

    public double ViewportHeight { get; set; }

    public double ViewportWidth { get; set; }

    public void ScrollToPage(int pageNumber)
    {
        _context?.ScrollToPage(pageNumber);
    }

    public void LineDown()
    {
        SetVerticalOffset(VerticalOffset + ScrollTick);
    }

    public void LineUp()
    {
        SetVerticalOffset(VerticalOffset - ScrollTick);
    }

    public void LineLeft()
    {
        SetHorizontalOffset(HorizontalOffset - ScrollTick);
    }

    public void LineRight()
    {
        SetHorizontalOffset(HorizontalOffset + ScrollTick);
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        return rectangle;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            OnMouseTilt(HiWord(wParam));
            return (IntPtr)1;
        }

        return IntPtr.Zero;
    }

    public static int HiWord(IntPtr ptr)
    {
        return unchecked((short)((uint)GetIntUnchecked(ptr) >> 16));
    }

    public static int GetIntUnchecked(IntPtr value)
    {
        return IntPtr.Size == sizeof(long) ? unchecked((int)value.ToInt64()) : value.ToInt32();
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

    public void ZoomIn()
    {
        Scale = Scale + (Scale * ScaleFactor);
    }

    public void ZoomOut()
    {
        Scale = Scale - (Scale * ScaleFactor);
    }

    private void OnScaleChanged()
    {
        if (_context == null)
        {
            return;
        }

        float centerX;
        float centerY;

        if (IsMouseOver)
        {
            var mousePosition = Mouse.GetPosition(this);
            centerX = (float)(mousePosition.X * CanvasScale.X);
            centerY = (float)(mousePosition.Y * CanvasScale.Y);
        }
        else
        {
            centerX = _context.ViewportWidth / 2;
            centerY = _context.ViewportHeight / 2;
        }

        _context.UpdateScalePreserveOffset((float)Scale, centerX, centerY);
        InvalidateVisual();
    }

    public void MouseWheelLeft()
    {
        SetHorizontalOffset(HorizontalOffset - (ScrollTick * Scale));
    }

    public void MouseWheelRight()
    {
        SetHorizontalOffset(HorizontalOffset + (ScrollTick * Scale));
    }

    public void PageDown()
    {
        if (_context != null)
        {
            SetVerticalOffset(VerticalOffset + _context.ViewportHeight);
        }
    }

    public void PageUp()
    {
        if (_context != null)
        {
            SetVerticalOffset(VerticalOffset - _context.ViewportHeight);
        }
    }

    public void PageLeft()
    {
        if (_context != null)
        {
            SetHorizontalOffset(HorizontalOffset - _context.ViewportWidth);
        }
    }

    public void PageRight()
    {
        if (_context != null)
        {
            SetHorizontalOffset(HorizontalOffset + _context.ViewportWidth);
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
