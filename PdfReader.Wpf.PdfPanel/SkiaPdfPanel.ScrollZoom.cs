using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System;
using System.Windows.Input;
using System.Linq;

namespace PdfReader.Wpf.PdfPanel
{
    /// <summary>
    /// Contains implementation of the IScrollInfo interface and methods for scrolling and zooming.
    /// </summary>
    public partial class SkiaPdfPanel : IScrollInfo
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        private bool autoScaling;

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
            if (Pages == null)
            {
                return;
            }

            double verticalOffset = 0;

            for (int i = 0; i < Pages.Count; i++)
            {
                var page = Pages[i];

                if (page.PageNumber == pageNumber)
                {
                    break;
                }

                verticalOffset += page.Info.GetRotatedSize(page.UserRotation).Height + PageGap;
            }

            SetVerticalOffset(verticalOffset * Scale);
        }

        public double GetCenterOffset()
        {
            var centerOffset = (CanvasSize.Width - ExtentWidth) / 2;

            if (centerOffset < 0)
            {
                centerOffset = 0;
            }

            return centerOffset;
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

        private void UpdateAutoScale()
        {
            if (Pages == null)
            {
                return;
            }

            switch (AutoScaleMode)
            {
                case PdfPanelAutoScaleMode.NoAutoScale:
                    {
                        break;
                    }
                case PdfPanelAutoScaleMode.ScaleToWidth:
                    {
                        var maxVisibleWidth = Pages.Max(x => x.Info.GetRotatedSize(x.UserRotation).Width) + PagesPadding.Left + PagesPadding.Right + 1;
                        var scale = CanvasSize.Width / maxVisibleWidth;
                        SetAutoScale(scale);
                        break;
                    }
                case PdfPanelAutoScaleMode.ScaleToVisible:
                    {
                        var maxVisibleWidth = GetVisiblePages().Max(x => x.RotatedSize.Width) + PagesPadding.Left + PagesPadding.Right + 1;
                        var scale = CanvasSize.Width / maxVisibleWidth;

                        SetAutoScale(scale);
                        break;
                    }
            }
        }

        private void UpdateScrollInfo()
        {
            if (Pages == null || ScrollOwner == null)
            {
                return;
            }

            var size = Pages.GetAreaSize(PageGap);

            ExtentHeight = size.Height * Scale + PagesPadding.Top * Scale + PagesPadding.Bottom * Scale;
            ExtentWidth = size.Width * Scale + PagesPadding.Left * Scale + PagesPadding.Right * Scale;
            ViewportWidth = CanvasSize.Width;
            ViewportHeight = CanvasSize.Height;

            ScrollOwner.InvalidateScrollInfo();
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
            Scale = Clamp(Scale + Scale * ScaleFactor, MinScale, MaxScale);
        }

        public void ZoomOut()
        {
            Scale = Clamp(Scale - Scale * ScaleFactor, MinScale, MaxScale);
        }

        private void OnScaleChanged(double oldScale)
        {
            UpdateScrollInfo();
            UpdatePositionOnZoom(oldScale);
            ValidateMargins();
        }

        private void UpdatePositionOnZoom(double oldScale)
        {
            double centerY;
            double centerX;

            if (IsMouseOver)
            {
                var mousePosition = Mouse.GetPosition(this);

                centerY = mousePosition.Y * CanvasSize.Height / ActualHeight;
                centerX = mousePosition.X * CanvasSize.Width / ActualWidth;
            }
            else
            {
                centerY = CanvasSize.Height / 2;
                centerX = CanvasSize.Width / 2;
            }

            UpdatePositionOnZoom(oldScale, centerX, centerY);
        }

        private void UpdatePositionOnZoom(double oldScale, double centerX, double centerY)
        {
            var centerVerticalDiff = centerY / oldScale * Scale - centerY;
            SetVerticalOffset(VerticalOffset / oldScale * Scale + centerVerticalDiff);

            var centerHorizontalDiff = centerX / oldScale * Scale - centerX;
            SetHorizontalOffset(HorizontalOffset / oldScale * Scale + centerHorizontalDiff);
        }

        public void MouseWheelLeft()
        {
            SetHorizontalOffset(HorizontalOffset - ScrollTick);
        }

        public void MouseWheelRight()
        {
            SetHorizontalOffset(HorizontalOffset + ScrollTick);
        }

        public void PageDown()
        {
            SetVerticalOffset(VerticalOffset + CanvasSize.Height);
        }

        public void PageUp()
        {
            SetVerticalOffset(VerticalOffset - CanvasSize.Height);
        }

        public void PageLeft()
        {
            SetHorizontalOffset(HorizontalOffset - CanvasSize.Width);
        }

        public void PageRight()
        {
            SetHorizontalOffset(HorizontalOffset + CanvasSize.Width);
        }

        public void SetHorizontalOffset(double offset)
        {
            HorizontalOffset = offset;
            ValidateMargins();
            InvalidateVisual();
        }

        public void SetVerticalOffset(double offset)
        {
            VerticalOffset = offset;
            ValidateMargins();
            InvalidateVisual();
        }

        private void ValidateMargins()
        {
            var scrollHeight = Math.Max(0, ExtentHeight - ViewportHeight);
            VerticalOffset = Clamp(VerticalOffset, 0, scrollHeight);

            var scrollWidth = Math.Max(0, ExtentWidth - ViewportWidth);
            HorizontalOffset = Clamp(HorizontalOffset, 0, scrollWidth);
        }

        private void SetAutoScale(double scale)
        {
            if (scale == Scale)
            {
                return;
            }

            autoScaling = true;

            var oldScale = Scale;
            Scale = scale;

            UpdateScrollInfo();
            UpdatePositionOnZoom(oldScale, 0, 0);
            HorizontalOffset = (ExtentWidth - ViewportWidth) / 2;

            autoScaling = false;
        }

        public static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
