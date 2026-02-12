using PdfPixel.PdfPanel.Extensions;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf
{
    /// <summary>
    /// Contains implementation of the IScrollInfo interface and methods for scrolling and zooming.
    /// </summary>
    public partial class WpfPdfPanel : IScrollInfo
    {
        const int WM_MOUSEHWHEEL = 0x020E;

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
            if (_context != null)
            {
                _context.ScrollToPage(pageNumber);
            }
        }

        public void LineDown()
        {
            if (_context != null)
            {
                _context.VerticalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineUp()
        {
            if (_context != null)
            {
                _context.VerticalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineLeft()
        {
            if (_context != null)
            {
                _context.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineRight()
        {
            if (_context != null)
                {
                _context.HorizontalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            return rectangle;
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_MOUSEHWHEEL:
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
                if (_context != null)
                {
                    _context.VerticalOffset += ScrollTick;
                    InvalidateVisual();
                }
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
                if (_context != null)
                {
                    _context.VerticalOffset -= ScrollTick;
                    InvalidateVisual();
                }
            }
        }

        public void ZoomIn()
        {
            Scale = Scale + Scale * ScaleFactor;
        }

        public void ZoomOut()
        {
            Scale = Scale - Scale * ScaleFactor;
        }

        private void OnScaleChanged()
        {
            if (_context != null)
            {
                float centerY;
                float centerX;

                if (IsMouseOver)
                {
                    var mousePosition = Mouse.GetPosition(this);

                    centerY = (float)(mousePosition.Y * CanvasScale.X);
                    centerX = (float)(mousePosition.X * CanvasScale.Y);
                }
                else
                {
                    centerY = _context.ViewportHeight / 2;
                    centerX = _context.ViewportWidth / 2;
                }

                _context.UpdateScalePreserveOffset((float)Scale, centerX, centerY);
                InvalidateVisual();
            }
        }

        public void MouseWheelLeft()
        {
            if (_context != null)
            {
                _context.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void MouseWheelRight()
        {
            if (_context != null)
            {
                _context.HorizontalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void PageDown()
        {
            if (_context != null)
            {
                _context.VerticalOffset += _context.ViewportHeight;
                InvalidateVisual();
            }
        }

        public void PageUp()
        {
            if (_context != null)
            {
                _context.VerticalOffset -= _context.ViewportHeight;
                InvalidateVisual();
            }
        }

        public void PageLeft()
        {
            if (_context != null)
            {
                _context.HorizontalOffset -= _context.ViewportWidth;
                InvalidateVisual();
            }
        }

        public void PageRight()
        {
            if (_context != null)
            {
                _context.HorizontalOffset += _context.ViewportWidth;
                InvalidateVisual();
            }
        }

        public void SetVerticalOffset(double offset)
        {
            if (_context != null)
            {
                _context.VerticalOffset = (float)offset;
                InvalidateVisual();
            }
        }

        public void SetHorizontalOffset(double offset)
        {
            if (_context != null)
            {
                _context.HorizontalOffset = (float)offset;
                InvalidateVisual();
            }
        }
    }
}
