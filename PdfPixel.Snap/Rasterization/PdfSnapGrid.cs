using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Imaging;
using System;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// How shapes and images meet the device pixel grid (ISO 32000-2 10.7.4): an axis-aligned image or rectangle paints the
/// pixels whose centers it holds (at least one per axis), and anything else is antialiased.
/// </summary>
public static class PdfSnapGrid
{
    // Distance in device pixels within which coordinates computed in float count as the same, so that a
    // rectangle placed exactly on a pixel boundary does not gain a pixel from rounding error.
    private const float CoordinateTolerance = 1f / 1024;

    // Bound on pixel coordinates, far outside any surface, so that huge or infinite geometry stays an int.
    private const double CoordinateLimit = 1 << 24;

    /// <summary>
    /// Maps <paramref name="bounds"/> through <paramref name="deviceMatrix"/>, puts every edge on the whole device pixel
    /// nearest to it, and maps the result back.
    /// </summary>
    public static PdfRectangle SnapToWholeDevicePixels(in PdfRectangle bounds, in PdfMatrix deviceMatrix)
    {
        PdfRectangle deviceRect = SnapToDevicePixels(deviceMatrix.MapRect(bounds));

        return deviceMatrix.Invert().MapRect(deviceRect);
    }

    /// <summary>
    /// Puts every edge of <paramref name="deviceRect"/> on the whole device pixel nearest to it, and gives an axis whose
    /// edges rounded onto the same boundary a whole pixel of its own, so that a mark too thin to round to anything still
    /// covers a row. The rescued axis keeps the edge it rounded to and grows away from it, so the edge geometry abutting
    /// this one shares stays shared.
    /// </summary>
    internal static PdfRectangle SnapToDevicePixels(in PdfRectangle deviceRect)
    {
        float left = SnapToWholePixel(deviceRect.Left);
        float top = SnapToWholePixel(deviceRect.Top);

        return new PdfRectangle(
            left,
            top,
            MathF.Max(SnapToWholePixel(deviceRect.Right), left + 1f),
            MathF.Max(SnapToWholePixel(deviceRect.Bottom), top + 1f));
    }

    /// <summary>
    /// Rounds one device coordinate to the whole pixel nearest to it, taking a coordinate exactly halfway to the higher
    /// one. Each coordinate is rounded on its own, so every command sends a given coordinate to the same pixel whatever
    /// geometry it belongs to, and two rectangles sharing an edge come out still sharing it rather than with a gap or an
    /// overlap between them. Rounding a halfway coordinate the same way wherever it sits is what keeps a rectangle a
    /// whole number of pixels wide from changing width as it moves across the grid.
    /// </summary>
    public static float SnapToWholePixel(float deviceCoordinate) => MathF.Floor(deviceCoordinate + 0.5f);

    /// <summary>
    /// Returns the pixels a filled <paramref name="path"/> paints when it is an axis-aligned rectangle, otherwise
    /// <see langword="null"/>.
    /// </summary>
    internal static PdfIntegerRectangle? GetShapePixels(PdfPath path, in PdfMatrix deviceMatrix)
    {
        PdfPathInfo pathInfo = path.GetPathInfo();

        if (pathInfo.IsRectangle)
        {
            return GetRectanglePixels(pathInfo.Bounds, deviceMatrix);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the pixels whose centers a filled <paramref name="rectangle"/> holds, at least one per axis, or
    /// <see langword="null"/> when the transformation turns or skews it.
    /// </summary>
    internal static PdfIntegerRectangle? GetRectanglePixels(in PdfRectangle rectangle, in PdfMatrix deviceMatrix)
    {
        PdfRectangle? deviceRectangle = MapAxisAligned(rectangle, deviceMatrix);

        if (deviceRectangle != null)
        {
            return GetCenteredPixels(deviceRectangle.Value);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the pixels whose centers lie in an image's <paramref name="footprint"/>, at least one per axis, or
    /// <see langword="null"/> when the transformation turns or skews it.
    /// </summary>
    internal static PdfIntegerRectangle? GetImagePixels(in PdfRectangle footprint, in PdfMatrix deviceMatrix)
    {
        PdfRectangle? deviceRectangle = MapAxisAligned(footprint, deviceMatrix);

        if (deviceRectangle != null)
        {
            return GetCenteredPixels(deviceRectangle.Value);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns whether the unit square <paramref name="unitToDevice"/> maps keeps its axes along the device's, neither
    /// skewed nor turned by a quarter, within the grid's coordinate tolerance.
    /// </summary>
    internal static bool IsUnturned(in PdfMatrix unitToDevice)
        => Math.Abs(unitToDevice.SkewX) <= CoordinateTolerance && Math.Abs(unitToDevice.SkewY) <= CoordinateTolerance;

    /// <summary>
    /// Replaces the content of <paramref name="edges"/> with the device outline of an image footprint, at least one
    /// pixel thick across each of its axes: a thinner footprint is widened about its middle to one pixel.
    /// </summary>
    internal static void AddImageFootprint(SnapEdgeList edges, in PdfRectangle footprint, in PdfMatrix deviceMatrix)
    {
        PdfSize axisLengths = GetDeviceAxisLengths(deviceMatrix);
        float determinant = (deviceMatrix.ScaleX * deviceMatrix.ScaleY) - (deviceMatrix.SkewX * deviceMatrix.SkewY);
        float area = Math.Abs(determinant) * footprint.Width * footprint.Height;
        float halfWidth = footprint.Width * GetWideningScale(area, axisLengths.Height * footprint.Height) * 0.5f;
        float halfHeight = footprint.Height * GetWideningScale(area, axisLengths.Width * footprint.Width) * 0.5f;
        PdfRectangle widened = new(footprint.MidX - halfWidth, footprint.MidY - halfHeight, footprint.MidX + halfWidth, footprint.MidY + halfHeight);

        edges.Clear();
        edges.AddQuadrilateral(
            deviceMatrix.MapPoint(new PdfPoint(widened.Left, widened.Top)),
            deviceMatrix.MapPoint(new PdfPoint(widened.Right, widened.Top)),
            deviceMatrix.MapPoint(new PdfPoint(widened.Right, widened.Bottom)),
            deviceMatrix.MapPoint(new PdfPoint(widened.Left, widened.Bottom)));
    }

    /// <summary>
    /// Returns how the columns of <paramref name="pixels"/> map onto an axis-aligned image's sample columns, in sample
    /// order: from the rightmost pixel when the image is mirrored.
    /// </summary>
    internal static PdfImageRowAxisMapping GetImageColumnMapping(int sampleCount, in PdfIntegerRectangle pixels, in PdfMatrix unitToDevice)
    {
        float scale = unitToDevice.ScaleX;
        int firstPixel = (scale < 0) ? pixels.Right - 1 : pixels.Left;
        float firstCenter = GetSamplePosition(sampleCount, firstPixel, unitToDevice.TranslateX, scale);

        return new PdfImageRowAxisMapping(sampleCount, pixels.Width, firstCenter, sampleCount / Math.Abs(scale));
    }

    /// <summary>
    /// Returns how the rows of <paramref name="pixels"/> map onto an axis-aligned image's sample rows, in device order:
    /// counted from the bottom sample row when the image's rows run up the device.
    /// </summary>
    internal static PdfImageRowAxisMapping GetImageRowMapping(int sampleCount, in PdfIntegerRectangle pixels, in PdfMatrix unitToDevice)
    {
        float scale = unitToDevice.ScaleY;
        float firstCenter = GetSamplePosition(sampleCount, pixels.Top, unitToDevice.TranslateY, scale);

        if (scale < 0)
        {
            firstCenter = sampleCount - firstCenter;
        }

        return new PdfImageRowAxisMapping(sampleCount, pixels.Height, firstCenter, sampleCount / Math.Abs(scale));
    }

    /// <summary>
    /// Returns the grid a turned or skewed image is resized to: as many samples per axis as the axis spans device
    /// pixels, over the part of the image <paramref name="pixels"/> reaches.
    /// </summary>
    internal static SnapResizedImageGrid GetResizedImageGrid(in PdfIntegerSize sampleSize, in PdfIntegerRectangle pixels, in PdfMatrix unitToDevice)
    {
        PdfMatrix deviceToUnit = unitToDevice.Invert();
        PdfIntegerSize resizedSize = GetResizedSize(unitToDevice);
        int width = resizedSize.Width;
        int height = resizedSize.Height;
        bool readsBottomUp = deviceToUnit.ScaleY < 0;
        PdfRectangle unitRegion = deviceToUnit.MapRect(new PdfRectangle(pixels.Left, pixels.Top, pixels.Right, pixels.Bottom));
        float regionTop = unitRegion.Top;
        float regionBottom = unitRegion.Bottom;

        if (readsBottomUp)
        {
            regionTop = 1 - unitRegion.Bottom;
            regionBottom = 1 - unitRegion.Top;
        }

        int columnStart = Math.Max(0, Math.Min(width - 1, (int)Math.Floor(unitRegion.Left * width)));
        int columnEnd = Math.Max(columnStart + 1, Math.Min(width, (int)Math.Ceiling(unitRegion.Right * width)));
        int rowStart = Math.Max(0, Math.Min(height - 1, (int)Math.Floor(regionTop * height)));
        int rowEnd = Math.Max(rowStart + 1, Math.Min(height, (int)Math.Ceiling(regionBottom * height)));
        float columnStep = sampleSize.Width / (float)width;
        float rowStep = sampleSize.Height / (float)height;
        PdfImageRowAxisMapping horizontal = new(sampleSize.Width, columnEnd - columnStart, (columnStart + 0.5f) * columnStep, columnStep);
        PdfImageRowAxisMapping vertical = new(sampleSize.Height, rowEnd - rowStart, (rowStart + 0.5f) * rowStep, rowStep);
        PdfMatrix deviceToResized;

        if (readsBottomUp)
        {
            deviceToResized = deviceToUnit
                .PostConcat(PdfMatrix.CreateScale(width, -height))
                .PostConcat(PdfMatrix.CreateTranslation(-columnStart, height - rowStart));
        }
        else
        {
            deviceToResized = deviceToUnit
                .PostConcat(PdfMatrix.CreateScale(width, height))
                .PostConcat(PdfMatrix.CreateTranslation(-columnStart, -rowStart));
        }

        return new SnapResizedImageGrid(horizontal, vertical, readsBottomUp, deviceToResized);
    }

    /// <summary>
    /// Returns the size a turned or skewed image is resized to: as many samples per axis as the axis spans device
    /// pixels.
    /// </summary>
    internal static PdfIntegerSize GetResizedSize(in PdfMatrix unitToDevice)
    {
        PdfSize axisLengths = GetDeviceAxisLengths(unitToDevice);
        int width = Math.Max(1, (int)Math.Round(axisLengths.Width));
        int height = Math.Max(1, (int)Math.Round(axisLengths.Height));

        return new PdfIntegerSize(width, height);
    }

    /// <summary>
    /// Returns the device length of one unit along each axis of the space <paramref name="deviceMatrix"/> maps to the
    /// device.
    /// </summary>
    internal static PdfSize GetDeviceAxisLengths(in PdfMatrix deviceMatrix)
    {
        float xAxisLength = MathF.Sqrt((deviceMatrix.ScaleX * deviceMatrix.ScaleX) + (deviceMatrix.SkewY * deviceMatrix.SkewY));
        float yAxisLength = MathF.Sqrt((deviceMatrix.SkewX * deviceMatrix.SkewX) + (deviceMatrix.ScaleY * deviceMatrix.ScaleY));

        return new PdfSize(xAxisLength, yAxisLength);
    }

    /// <summary>
    /// Returns the smallest whole-pixel rectangle holding <paramref name="rectangle"/>.
    /// </summary>
    internal static PdfIntegerRectangle RoundOut(in PdfRectangle rectangle)
    {
        return new(
            ToIndex(Math.Floor(rectangle.Left)),
            ToIndex(Math.Floor(rectangle.Top)),
            ToIndex(Math.Ceiling(rectangle.Right)),
            ToIndex(Math.Ceiling(rectangle.Bottom)));
    }

    private static PdfRectangle? MapAxisAligned(in PdfRectangle rectangle, in PdfMatrix deviceMatrix)
    {
        PdfRectangle bounds = deviceMatrix.MapRect(rectangle);
        bool isAxisAligned = IsBoundsCorner(deviceMatrix.MapPoint(new PdfPoint(rectangle.Left, rectangle.Top)), bounds)
            && IsBoundsCorner(deviceMatrix.MapPoint(new PdfPoint(rectangle.Right, rectangle.Top)), bounds)
            && IsBoundsCorner(deviceMatrix.MapPoint(new PdfPoint(rectangle.Right, rectangle.Bottom)), bounds)
            && IsBoundsCorner(deviceMatrix.MapPoint(new PdfPoint(rectangle.Left, rectangle.Bottom)), bounds);

        if (isAxisAligned)
        {
            return bounds;
        }
        else
        {
            return null;
        }
    }

    // ISO 32000-2 10.7.4 paints only the pixels whose centers lie in an image's footprint, so an image thinner than a
    // pixel can paint nothing. Writers draw rules and table borders as a 1x1 image stretched to a hairline, which
    // other viewers show at every zoom; by the spec they would come and go as the zoom changes. An image therefore
    // paints at least one pixel per axis: the one holding its middle.
    private static PdfIntegerRectangle GetCenteredPixels(in PdfRectangle bounds)
    {
        int left = ToIndex(Math.Ceiling(bounds.Left - 0.5 - CoordinateTolerance));
        int top = ToIndex(Math.Ceiling(bounds.Top - 0.5 - CoordinateTolerance));
        int right = ToIndex(Math.Ceiling(bounds.Right - 0.5 - CoordinateTolerance));
        int bottom = ToIndex(Math.Ceiling(bounds.Bottom - 0.5 - CoordinateTolerance));

        if (right <= left)
        {
            left = ToIndex(Math.Floor((bounds.Left + bounds.Right) * 0.5));
            right = left + 1;
        }

        if (bottom <= top)
        {
            top = ToIndex(Math.Floor((bounds.Top + bounds.Bottom) * 0.5));
            bottom = top + 1;
        }

        return new PdfIntegerRectangle(left, top, right, bottom);
    }

    private static bool IsBoundsCorner(in PdfPoint point, in PdfRectangle bounds)
    {
        bool onVerticalSide = Math.Abs(point.X - bounds.Left) <= CoordinateTolerance
            || Math.Abs(point.X - bounds.Right) <= CoordinateTolerance;
        bool onHorizontalSide = Math.Abs(point.Y - bounds.Top) <= CoordinateTolerance
            || Math.Abs(point.Y - bounds.Bottom) <= CoordinateTolerance;

        return onVerticalSide && onHorizontalSide;
    }

    private static float GetSamplePosition(int sampleCount, int pixel, float translate, float scale)
        => sampleCount * (pixel + 0.5f - translate) / scale;

    /// <summary>
    /// Returns the factor that widens one axis of a footprint of <paramref name="area"/> to one pixel across, the
    /// other axis being <paramref name="otherAxisLength"/> long; 1 when it is a pixel or more across, or has no area.
    /// </summary>
    private static float GetWideningScale(float area, float otherAxisLength)
    {
        if (area > 0 && area < otherAxisLength)
        {
            return otherAxisLength / area;
        }
        else
        {
            return 1;
        }
    }

    private static int ToIndex(double value) => (int)Math.Max(-CoordinateLimit, Math.Min(CoordinateLimit, value));
}
