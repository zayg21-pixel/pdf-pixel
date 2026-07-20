using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
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
        {
            Color = PdfColors.White,
            Alpha = fillPaint.Alpha,
            BlendMode = fillPaint.BlendMode
        };
    }

    /// <summary>
    /// Creates default background paint.
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    /// <param name="background">Background color.</param>
    public static PdfPaint CreateBackgroundPaint(in PdfColor background) => PdfPaint.Solid(background, PdfPaintStyle.Fill);

    /// <summary>
    /// Create paint for soft-mask application: composites the previously-rendered mask content onto
    /// the layer below via destination-in compositing, adding a luminosity-to-alpha color filter for
    /// <see cref="PdfSoftMaskSubtype.Luminosity"/> masks.
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPaint CreateSoftMaskPaint(PdfSoftMaskSubtype subtype)
    {
        PdfBlendMode blendMode = (subtype == PdfSoftMaskSubtype.Luminosity)
            ? PdfBlendMode.LuminosityMaskComposite
            : PdfBlendMode.MaskComposite;

        return new PdfPaint(PdfPaintStyle.Fill) { BlendMode = blendMode };
    }
}
