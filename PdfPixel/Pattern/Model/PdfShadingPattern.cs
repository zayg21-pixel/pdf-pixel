using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;

namespace PdfPixel.Pattern.Model;

/// <summary>
/// Represents a shading pattern (/PatternType 2) in a PDF document.
/// Provides access to the referenced shading and optional extended graphics state.
/// Caches the base shader for performance.
/// </summary>
public sealed class PdfShadingPattern : PdfPattern
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingPattern"/> class with the specified parameters.
    /// </summary>
    /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
    /// <param name="matrix">The pattern transformation matrix.</param>
    /// <param name="extGState">Optional graphics state parameters parsed from the pattern's /ExtGState entry (may be null).</param>
    internal PdfShadingPattern(
        PdfShading shading,
        in PdfMatrix matrix,
        PdfGraphicsStateParameters? extGState)
        : base(matrix, PdfPatternType.Shading)
    {
        Shading = shading;
        ExtGState = extGState;
    }

    /// <summary>
    /// Gets the shading object referenced by the pattern's /Shading entry.
    /// </summary>
    public PdfShading Shading { get; }

    /// <summary>
    /// Gets the graphics state parameters put into effect while the pattern is painted, or
    /// <see langword="null"/> when the pattern declares no /ExtGState.
    /// </summary>
    internal PdfGraphicsStateParameters? ExtGState { get; }

    /// <inheritdoc />
    internal override bool IsPageIndependent => ExtGState?.SoftMask == null;

    internal override void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        PdfGraphicsState shadingState = state;

        if (ExtGState != null)
        {
            shadingState = state.Clone();
            ExtGState.ApplyToGraphicsState(shadingState);
        }

        PdfMatrix matrix = PdfMatrix.Concat(shadingState.CTM.Invert(), PatternMatrix);

        PdfCommandRecorder recorder = new();

        recorder.Process(SaveStateCommand.Instance);
        recorder.Process(new ConcatMatrixCommand(matrix));

        if (Shading.BBox.HasValue)
        {
            recorder.Process(new ClipRectangleCommand(Shading.BBox.Value, PdfClipOperation.Intersect));
        }

        if (Shading.Background != null && Shading.BBox.HasValue)
        {
            PdfColorSpaceConverter? colorSpace = shadingState.Page.Cache.ColorSpace.Resolve(Shading.ColorSpaceReference) ?? PdfDeviceRgbColorSpaceConverter.Instance;
            PdfColor backgroundColor = colorSpace.ToSrgb(Shading.Background, shadingState.RenderingIntent, shadingState.TransferFunction);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(Shading.BBox.Value);

            recorder.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        PdfShadingContent content = Shading.GetContent(shadingState);

        recorder.Process(new DrawShadingCommand(content, shadingState.FillPaint.Alpha));

        recorder.Process(RestoreStateCommand.Instance);

        renderTarget.BeforePatternRender(processor, GetPatternBounds(content, matrix));
        processor.Process(new DrawRecordingCommand(recorder));
        renderTarget.AfterPatternRender(processor);
    }

    private static PdfRectangle? GetPatternBounds(PdfShadingContent content, in PdfMatrix matrix)
    {
        PdfRectangle? bounds = content.GetBounds();

        if (bounds == null)
        {
            return null;
        }

        return matrix.MapRect(bounds.Value);
    }
}
