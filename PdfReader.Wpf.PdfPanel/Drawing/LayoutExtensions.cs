using System;
using System.Windows;
using System.Windows.Media;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Extensions for <see cref="FrameworkElement"/> to work it's dimensions and layout to display drawing canvas.
    /// </summary>
    public static class LayoutExtensions
    {
        public static (Size Size, Point Scale, Point Offset) MeasureCanvas(this FrameworkElement element, Size finalSize)
        {
            var presentationSource = PresentationSource.FromVisual(element);
            Matrix transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
            var visualElement = presentationSource.RootVisual as UIElement;

            var scale = new Point(transformToDevice.M11, transformToDevice.M22);
            var size = new Size(Math.Round(finalSize.Width * scale.X), Math.Round(finalSize.Height * scale.Y));

            if (size.Width == 0 || size.Height == 0 || visualElement == null)
            {
                return (Size.Empty, new Point(), new Point());
            }
            else
            {

                var controlOffset = element.TranslatePoint(new Point(0, 0), visualElement);
                return (size, scale, controlOffset);
            }
        }

        public static bool IsCanvasSizeValid(this FrameworkElement element, Size size)
        {
            return size.Width > 1 && size.Height > 1 && !double.IsInfinity(size.Width) && !double.IsInfinity(size.Height);
        }

        public static double SnapPosition(this FrameworkElement element, double position, double scale)
        {
            var pixelSize = 1 / scale;
            var halfPixel = pixelSize / 2;
            var pixelOffset = position % pixelSize;

            if (pixelOffset < halfPixel)
            {
                return -pixelOffset;
            }
            else
            {
                return pixelSize - pixelOffset;
            }
        }
    }
}
