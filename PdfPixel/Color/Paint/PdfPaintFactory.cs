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
    /// Layer paint for composition operations, all blending and composing operations are delegated to layer.
    /// Alpha and blend mode come from the current fill paint; the color itself is irrelevant (opaque white)
    /// since the layer only exists to carry compositing parameters.
    /// </summary>
    public static PdfPaint CreateCompositionLayerPaint(PdfGraphicsState state)
    {
        PdfPaint fillPaint = state.FillPaint;
        return new PdfPaint(PdfPaintStyle.Fill)
            .WithColor(PdfColors.White)
            .WithAlpha(fillPaint.Color.Alpha)
            .WithBlendMode(fillPaint.BlendMode);
    }

    /// <summary>
    /// Creates default background paint.
    /// Antialiasing is deferred to command Execute time via <see cref="Commands.Execution.PdfCommandExecutionContext"/>.
    /// </summary>
    /// <param name="background">Background color.</param>
    public static PdfPaint CreateBackgroundPaint(in PdfColor background) => PdfPaint.Solid(background, PdfPaintStyle.Fill);

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
        => PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Fill).WithMaskParameters(new PdfMaskPaintParameters(PdfMaskPaintKind.Shape, PdfSoftMaskSubtype.Alpha, null));

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

        return shapePaint.WithColor(PdfColors.White);
    }
}
