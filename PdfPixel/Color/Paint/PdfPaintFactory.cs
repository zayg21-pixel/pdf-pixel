using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Rendering;
using PdfPixel.Transparency;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Factory for creating <see cref="PdfPaint"/> instances for PDF rendering.
/// </summary>
internal static class PdfPaintFactory
{
    /// <summary>
    /// DeviceGray level of white.
    /// </summary>
    public const float WhiteGray = 1f;

    /// <summary>
    /// Layer paint for composition operations, all blending and composing operations are delegated to layer.
    /// Alpha and blend mode come from the current fill paint; the color itself is irrelevant (opaque white)
    /// since the layer only exists to carry compositing parameters.
    /// </summary>
    public static PdfPaint CreateCompositionLayerPaint(PdfGraphicsState state)
    {
        PdfPaint fillPaint = state.FillPaint;
        return WithGray(new PdfPaint(PdfPaintStyle.Fill), WhiteGray)
            .WithAlpha(fillPaint.Alpha)
            .WithBlendMode(fillPaint.BlendMode);
    }

    /// <summary>
    /// Returns <paramref name="paint"/> with its color replaced by <paramref name="gray"/> in DeviceGray, with no device
    /// color mapping, clearing any pattern.
    /// </summary>
    /// <param name="paint">The paint to recolor.</param>
    /// <param name="gray">The gray level, 0 for black and 1 for white.</param>
    public static PdfPaint WithGray(PdfPaint paint, float gray)
        => paint.WithSolidColor(PdfDeviceGrayColorSpaceConverter.Instance, PdfDeviceColorMapping.None, new PdfColor([gray]));

    /// <summary>
    /// Creates opaque background paint of <paramref name="components"/> in <paramref name="colorSpace"/>, converted with the
    /// color conversion of the fill paint of <paramref name="state"/> and the device color mapping of its resources.
    /// Antialiasing is deferred to command Execute time via <see cref="Commands.Execution.PdfCommandExecutionContext"/>.
    /// </summary>
    /// <param name="state">Graphics state the background is drawn in.</param>
    /// <param name="colorSpace">Color space the background color is given in.</param>
    /// <param name="components">Background color components.</param>
    public static PdfPaint CreateBackgroundPaint(PdfGraphicsState state, PdfColorSpaceConverter colorSpace, float[] components)
    {
        PdfPaint fillPaint = state.FillPaint;

        return new PdfPaint(PdfPaintStyle.Fill)
            .WithColorConversion(fillPaint.RenderingIntent, fillPaint.UseBlackPointCompensation, fillPaint.TransferFunction)
            .WithSolidColor(colorSpace, state.Host.Resources.DeviceColorMapping, new PdfColor(components));
    }

    /// <summary>
    /// Create paint for soft-mask application: composites the previously-rendered mask content onto
    /// the layer below. <paramref name="subtype"/> and <paramref name="transferFunction"/> travel with
    /// the paint as <see cref="PdfMaskPaintParameters"/>, so the Skia conversion can build the
    /// subtype-appropriate color filter (luminosity-to-alpha, plus the transfer function) at replay time.
    /// Antialiasing is deferred to command Execute time via <see cref="Commands.Execution.PdfCommandExecutionContext"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPaint CreateSoftMaskPaint(PdfSoftMaskSubtype subtype, TransferFunctionTransform? transferFunction)
        => new PdfPaint(PdfPaintStyle.Fill).WithMaskParameters(new PdfMaskPaintParameters(PdfMaskPaintKind.SoftMask, subtype, transferFunction));

    /// <summary>
    /// Create opaque white paint that masks a pattern painted beneath with a shape, so the pattern shows within the shape
    /// without being clipped to it: the paint of the layer a path or text target draws its shape into, or the fill a stencil
    /// image is drawn with over the whole area of the pattern.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPaint CreateShapeMaskPaint()
        => WithGray(new PdfPaint(PdfPaintStyle.Fill), WhiteGray).WithMaskParameters(new PdfMaskPaintParameters(PdfMaskPaintKind.Shape, PdfSoftMaskSubtype.Alpha, null));

    /// <summary>
    /// Create paint that draws a shape opaque into the layer of <see cref="CreateShapeMaskPaint"/>: opaque white, with the
    /// style of <paramref name="paint"/> and its stroke styling when it strokes, and no pattern, alpha or blend mode.
    /// </summary>
    /// <param name="paint">The paint the shape is filled or stroked with.</param>
    public static PdfPaint CreateMaskShapePaint(PdfPaint paint)
    {
        PdfPaint shapePaint = (paint.Style == PdfPaintStyle.Stroke)
            ? new PdfPaint(paint.RequireStrokeStyle())
            : new PdfPaint(PdfPaintStyle.Fill);

        return WithGray(shapePaint, WhiteGray);
    }
}
