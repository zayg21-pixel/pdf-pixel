using PdfPixel.PdfPanel.Extensions;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PdfPixel.PdfPanel.Wpf
{
    /// <summary>
    /// Contains implementation of the IScrollInfo interface and methods for scrolling and zooming.
    /// </summary>
    public partial class WpfPdfPanel : IScrollInfo
    {
        private AnimationClock _verticalOffsetAnimationClock;
        private AnimationClock _horizontalOffsetAnimationClock;
        private const double ScrollAnimationDuration = 200; // milliseconds
        private bool _enableScrollAnimation = false;

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
                AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() + ScrollTick);
            }
        }

        public void LineUp()
        {
            if (_context != null)
            {
                AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() - ScrollTick);
            }
        }

        public void LineLeft()
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() - ScrollTick);
            }
        }

        public void LineRight()
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() + ScrollTick);
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
                    AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() + ScrollTick * Scale);
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
                    AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() - ScrollTick * Scale);
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
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() - ScrollTick * Scale);
            }
        }

        public void MouseWheelRight()
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() + ScrollTick * Scale);
            }
        }

        public void PageDown()
        {
            if (_context != null)
            {
                AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() + _context.ViewportHeight);
            }
        }

        public void PageUp()
        {
            if (_context != null)
            {
                AnimateVerticalOffset(GetCurrentVerticalOffsetTarget() - _context.ViewportHeight);
            }
        }

        public void PageLeft()
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() - _context.ViewportWidth);
            }
        }

        public void PageRight()
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(GetCurrentHorizontalOffsetTarget() + _context.ViewportWidth);
            }
        }


        /// <summary>
        /// Sets the vertical offset.
        /// </summary>
        /// <param name="offset">The target vertical offset.</param>
        public void SetVerticalOffset(double offset)
        {
            if (_context != null)
            {
                AnimateVerticalOffset(offset);
            }
        }


        /// <summary>
        /// Sets the horizontal offset.
        /// </summary>
        /// <param name="offset">The target horizontal offset.</param>
        public void SetHorizontalOffset(double offset)
        {
            if (_context != null)
            {
                AnimateHorizontalOffset(offset);
            }
        }
        /// <summary>
        /// Stores the current target for vertical offset animation.
        /// </summary>
        private double _verticalOffsetTarget;

        /// <summary>
        /// Stores the current target for horizontal offset animation.
        /// </summary>
        private double _horizontalOffsetTarget;

        /// <summary>
        /// Gets the current vertical offset animation target.
        /// </summary>
        public double GetCurrentVerticalOffsetTarget()
        {
            if (_verticalOffsetAnimationClock == null || _verticalOffsetAnimationClock.CurrentState != ClockState.Active)
            {
                return _context.VerticalOffset;
            }

            return _verticalOffsetTarget;
        }

        /// <summary>
        /// Gets the current horizontal offset animation target.
        /// </summary>
        public double GetCurrentHorizontalOffsetTarget()
        {
            if (_horizontalOffsetAnimationClock == null || _horizontalOffsetAnimationClock.CurrentState != ClockState.Active)
            {
                return _context.HorizontalOffset;
            }

            return _horizontalOffsetTarget;
        }

        public void AnimateVerticalOffset(double targetOffset)
        {
            if (_context == null)
            {
                return;
            }

            double startOffset = _context.VerticalOffset;
            if (!_enableScrollAnimation || Math.Abs(targetOffset - startOffset) < 0.5)
            {
                _verticalOffsetAnimationClock?.Controller?.Stop();
                _verticalOffsetTarget = targetOffset;
                _context.VerticalOffset = (float)targetOffset;
                InvalidateVisual();
                return;
            }

            _verticalOffsetTarget = targetOffset;
            var animation = new DoubleAnimation
            {
                From = startOffset,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(ScrollAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Create a clock for this animation and attach the invalidated handler to the clock
            _verticalOffsetAnimationClock?.Controller?.Stop();
            var clock = animation.CreateClock();
            clock.CurrentTimeInvalidated += (s, e) =>
            {
                // Use the specific clock for this animation to compute the current value.
                if (clock.CurrentState == ClockState.Active && _context != null)
                {
                    double value = animation.GetCurrentValue(startOffset, targetOffset, clock);
                    _context.VerticalOffset = (float)value;
                    InvalidateVisual();
                }
            };

            _verticalOffsetAnimationClock = clock;
            _verticalOffsetAnimationClock.Controller?.Begin();
        }

        /// <summary>
        /// Animates the horizontal offset to the specified value.
        /// </summary>
        /// <param name="targetOffset">The target horizontal offset.</param>
        public void AnimateHorizontalOffset(double targetOffset)
        {
            if (_context == null)
            {
                return;
            }

            double startOffset = _context.HorizontalOffset;
            if (!_enableScrollAnimation || Math.Abs(targetOffset - startOffset) < 0.5)
            {
                _horizontalOffsetAnimationClock?.Controller?.Stop();
                _horizontalOffsetTarget = targetOffset;
                _context.HorizontalOffset = (float)targetOffset;
                InvalidateVisual();
                return;
            }

            _horizontalOffsetTarget = targetOffset;
            var animation = new DoubleAnimation
            {
                From = startOffset,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(ScrollAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Create a clock for this animation and attach the invalidated handler to the clock
            _horizontalOffsetAnimationClock?.Controller?.Stop();
            var hClock = animation.CreateClock();
            hClock.CurrentTimeInvalidated += (s, e) =>
            {
                if (hClock.CurrentState == ClockState.Active && _context != null)
                {
                    double value = animation.GetCurrentValue(startOffset, targetOffset, hClock);
                    _context.HorizontalOffset = (float)value;
                    InvalidateVisual();
                }
            };

            _horizontalOffsetAnimationClock = hClock;
            _horizontalOffsetAnimationClock.Controller?.Begin();
        }
    }
}
