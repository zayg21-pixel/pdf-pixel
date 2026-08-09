using PdfPixel.Geometry;
using System;
using System.Numerics;

namespace PdfPixel.Commands;

/// <summary>
/// <para>
/// The pen a stroke leaves its mark with, as that mark lands on the device pixel grid: the width to
/// build the outline with, the matrix shaping the pen, and how thick the mark comes out in device
/// pixels.
/// </para>
/// <para>
/// A pen covering less than one device pixel is widened until it covers one, so that thin lines stay
/// visible when zoomed out; an axis it already covers keeps its width. The widening is the same on both
/// axes under a matrix that scales them alike, and the pen is a circle a single width describes. The two
/// differ under an anisotropic matrix, where the pen is an ellipse, and the matrix is what carries that
/// shape.
/// </para>
/// </summary>
internal readonly struct PdfDevicePen
{
    // Axes asking for nearly the same width are treated as one: the difference is invisible, and an
    // elliptical pen costs a transform on every point of the path where a circular one costs none.
    private const float UniformityTolerance = 0.01f;

    private PdfDevicePen(in PdfMatrix matrix, float width, float deviceThickness)
    {
        Matrix = matrix;
        Width = width;
        DeviceThickness = deviceThickness;
    }

    /// <summary>
    /// The matrix shaping the pen: identity for a circular pen, and a scaling matrix for the ellipse an
    /// anisotropic device matrix asks for.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// The line width to build the outline with, in the space the path is given in.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// How thick the mark the pen leaves is, in device pixels, across the axis carrying the least of it.
    /// </summary>
    public float DeviceThickness { get; }

    /// <summary>
    /// Computes the pen a stroke of <paramref name="lineWidth"/> is drawn with under the device matrix of
    /// <paramref name="executionContext"/>. A line width of zero or less is the PDF hairline, which comes
    /// out as exactly one device pixel on both axes.
    /// </summary>
    public static PdfDevicePen Create(PdfCommandExecutionContext executionContext, float lineWidth)
    {
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);

        // How many device pixels one unit along each axis of the path's own space is carried to.
        float deviceScaleX = new Vector2(deviceMatrix.ScaleX, deviceMatrix.SkewY).Length();
        float deviceScaleY = new Vector2(deviceMatrix.SkewX, deviceMatrix.ScaleY).Length();

        // A matrix mapping an axis onto a point leaves no mark on it to widen, and none to measure.
        if (deviceScaleX <= 0 || deviceScaleY <= 0)
        {
            return new PdfDevicePen(PdfMatrix.Identity, (lineWidth > 0) ? lineWidth : 1f, 0f);
        }

        // A pen covering less than one device pixel is widened to cover exactly one, and every other
        // pen keeps the width it was authored with. The hairline is that rule at a line width of zero.
        float widthX = (lineWidth * deviceScaleX < 1f) ? 1f / deviceScaleX : lineWidth;
        float widthY = (lineWidth * deviceScaleY < 1f) ? 1f / deviceScaleY : lineWidth;

        float wider = MathF.Max(widthX, widthY);

        if (MathF.Abs(widthX - widthY) <= UniformityTolerance * wider)
        {
            widthX = wider;
            widthY = wider;
        }

        // The width both axes share is a circular pen, which the outline builder reaches by building
        // wider; only what one axis has beyond the other has to go through the matrix.
        float sharedWidth = MathF.Min(widthX, widthY);

        return new PdfDevicePen(
            PdfMatrix.CreateScale(widthX / sharedWidth, widthY / sharedWidth),
            sharedWidth,
            MathF.Min(widthX * deviceScaleX, widthY * deviceScaleY));
    }
}
