using PdfPixel.Commands.Context;
using PdfPixel.Geometry;
using System;
using System.Numerics;

namespace PdfPixel.Commands.Processing;

/// <summary>
/// The pen a stroke is built with, as its mark lands on the device pixel grid: the outline width, the
/// matrix shaping the pen, and the resulting thickness in device pixels.
/// </summary>
internal readonly struct PdfDevicePen
{
    // Relative difference below which the two axis widths are treated as one.
    private const float UniformityTolerance = 0.01f;

    private PdfDevicePen(in PdfMatrix matrix, float width, float deviceThickness)
    {
        Matrix = matrix;
        Width = width;
        DeviceThickness = deviceThickness;
    }

    /// <summary>
    /// The matrix shaping the pen: identity for a circular pen, a scaling matrix for an elliptical one.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// The line width to build the outline with, in the space the path is given in.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Thickness of the pen's mark in device pixels, across the thinner axis.
    /// </summary>
    public float DeviceThickness { get; }

    /// <summary>
    /// Computes the pen a stroke of <paramref name="lineWidth"/> is drawn with under the device matrix of
    /// <paramref name="executionContext"/>. A line width of zero or less is the PDF hairline, one device
    /// pixel on both axes.
    /// </summary>
    public static PdfDevicePen Create(PdfCommandExecutionContext executionContext, float lineWidth)
    {
        PdfMatrix deviceMatrix = PdfCommandProcessingUtilities.GetScaledMatrix(executionContext);

        // Device pixels per unit along each axis of the path's own space.
        float deviceScaleX = new Vector2(deviceMatrix.ScaleX, deviceMatrix.SkewY).Length();
        float deviceScaleY = new Vector2(deviceMatrix.SkewX, deviceMatrix.ScaleY).Length();

        if (deviceScaleX <= 0 || deviceScaleY <= 0)
        {
            return new PdfDevicePen(PdfMatrix.Identity, (lineWidth > 0) ? lineWidth : 1f, 0f);
        }

        // A pen covering less than one device pixel is widened to cover exactly one.
        float widthX = (lineWidth * deviceScaleX < 1f) ? 1f / deviceScaleX : lineWidth;
        float widthY = (lineWidth * deviceScaleY < 1f) ? 1f / deviceScaleY : lineWidth;

        float wider = MathF.Max(widthX, widthY);

        if (MathF.Abs(widthX - widthY) <= UniformityTolerance * wider)
        {
            widthX = wider;
            widthY = wider;
        }

        // The shared width is a circular pen; the matrix carries the excess of the wider axis.
        float sharedWidth = MathF.Min(widthX, widthY);

        return new PdfDevicePen(
            PdfMatrix.CreateScale(widthX / sharedWidth, widthY / sharedWidth),
            sharedWidth,
            MathF.Min(widthX * deviceScaleX, widthY * deviceScaleY));
    }
}
