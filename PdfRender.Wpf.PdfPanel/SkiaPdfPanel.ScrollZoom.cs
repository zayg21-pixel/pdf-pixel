using PdfRender.Canvas;
using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfRender.Wpf.PdfPanel
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
            if (_viewerCanvas != null)
            {
                _viewerCanvas.ScrollToPage(pageNumber);
            }
        }

        public void LineDown()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.VerticalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineUp()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.VerticalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineLeft()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void LineRight()
        {
            if (_viewerCanvas != null)
                {
                _viewerCanvas.HorizontalOffset += ScrollTick;
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
                if (_viewerCanvas != null)
                {
                    _viewerCanvas.VerticalOffset += ScrollTick;
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
                if (_viewerCanvas != null)
                {
                    _viewerCanvas.VerticalOffset -= ScrollTick;
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
            if (_viewerCanvas != null)
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
                    centerY = _viewerCanvas.Height / 2;
                    centerX = _viewerCanvas.Width / 2;
                }

                _viewerCanvas.UpdateScalePreserveOffset((float)Scale, centerX, centerY);
                InvalidateVisual();
            }
        }

        public void MouseWheelLeft()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset -= ScrollTick;
                InvalidateVisual();
            }
        }

        public void MouseWheelRight()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset += ScrollTick;
                InvalidateVisual();
            }
        }

        public void PageDown()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.VerticalOffset += _viewerCanvas.Height;
                InvalidateVisual();
            }
        }

        public void PageUp()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.VerticalOffset -= _viewerCanvas.Height;
                InvalidateVisual();
            }
        }

        public void PageLeft()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset -= _viewerCanvas.Width;
                InvalidateVisual();
            }
        }

        public void PageRight()
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset += _viewerCanvas.Width;
                InvalidateVisual();
            }
        }

        public void SetVerticalOffset(double offset)
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.VerticalOffset = (float)offset;
                InvalidateVisual();
            }
        }

        public void SetHorizontalOffset(double offset)
        {
            if (_viewerCanvas != null)
            {
                _viewerCanvas.HorizontalOffset = (float)offset;
                InvalidateVisual();
            }
        }
    }
}
