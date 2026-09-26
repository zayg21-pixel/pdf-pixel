using PdfPixel.Geometry;
using System;
using System.Windows;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// Extensions for <see cref="FrameworkElement"/> to measure the panel surface and map host coordinates to it.
/// </summary>
internal static class LayoutExtensions
{
    public static (Size PanelSize, Point HostScale, Point HostOffset) MeasurePanel(this FrameworkElement element, Size finalSize)
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

    public static bool IsPanelSizeValid(this FrameworkElement element, Size size)
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

    /// <summary>
    /// Returns the matrix that maps host coordinates (device-independent units relative to <paramref name="element"/>)
    /// to panel pixels of the surface drawn at the snapped <paramref name="hostOffset"/>.
    /// </summary>
    public static PdfMatrix GetHostToPanelMatrix(this FrameworkElement element, in PdfPoint hostOffset, in PdfPoint hostScale)
    {
        float snapX = (float)element.SnapPosition(hostOffset.X, hostScale.X);
        float snapY = (float)element.SnapPosition(hostOffset.Y, hostScale.Y);

        return PdfMatrix.CreateTranslation(-snapX, -snapY)
            .PostConcat(PdfMatrix.CreateScale(hostScale.X, hostScale.Y));
    }
}
