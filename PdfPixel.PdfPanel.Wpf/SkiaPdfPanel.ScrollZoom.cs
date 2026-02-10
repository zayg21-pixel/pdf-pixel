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
    public partial class SkiaPdfPanel : IScrollInfo
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
            if (_viewerContext != null)
            {
                _viewerContext.ScrollToPage(pageNumber);
            }
        }

        public void LineDown()
        {
            if (_viewerContext != null)
            {
                _viewerContext.VerticalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineUp()
        {
            if (_viewerContext != null)
            {
                _viewerContext.VerticalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineLeft()
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineRight()
        {
            if (_viewerContext != null)
                {
                _viewerContext.HorizontalOffset += ScrollTick;
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
                if (_viewerContext != null)
                {
                    _viewerContext.VerticalOffset += ScrollTick;
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
                if (_viewerContext != null)
                {
                    _viewerContext.VerticalOffset -= ScrollTick;
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
            if (_viewerContext != null)
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
                    centerY = _viewerContext.Height / 2;
                    centerX = _viewerContext.Width / 2;
                }

                _viewerContext.UpdateScalePreserveOffset((float)Scale, centerX, centerY);
                InvalidateVisual();
            }
        }

        public void MouseWheelLeft()
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void MouseWheelRight()
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void PageDown()
        {
            if (_viewerContext != null)
            {
                _viewerContext.VerticalOffset += _viewerContext.Height;
                InvalidateVisual();
            }
        }

        public void PageUp()
        {
            if (_viewerContext != null)
            {
                _viewerContext.VerticalOffset -= _viewerContext.Height;
                InvalidateVisual();
            }
        }

        public void PageLeft()
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset -= _viewerContext.Width;
                InvalidateVisual();
            }
        }

        public void PageRight()
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset += _viewerContext.Width;
                InvalidateVisual();
            }
        }

        public void SetVerticalOffset(double offset)
        {
            if (_viewerContext != null)
            {
                _viewerContext.VerticalOffset = (float)offset;
                InvalidateVisual();
            }
        }

        public void SetHorizontalOffset(double offset)
        {
            if (_viewerContext != null)
            {
                _viewerContext.HorizontalOffset = (float)offset;
                InvalidateVisual();
            }
        }
    }
}
