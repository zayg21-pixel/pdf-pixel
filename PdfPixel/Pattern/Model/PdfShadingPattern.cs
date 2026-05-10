using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using SkiaSharp;

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
    /// <param name="page">The owning PDF page context.</param>
    /// <param name="sourceObject">The original source PDF object for the pattern.</param>
    /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
    /// <param name="matrix">The pattern transformation matrix.</param>
    /// <param name="extGState">Optional extended graphics state dictionary (may be null).</param>
    internal PdfShadingPattern(
        PdfObject sourceObject,
        PdfShading shading,
        SKMatrix matrix,
        PdfDictionary extGState)
        : base(sourceObject, matrix, PdfPatternType.Shading)
    {
        Shading = shading;
        ExtGState = extGState;
    }

    /// <summary>
    /// Gets the shading object referenced by the pattern's /Shading entry.
    /// </summary>
    public PdfShading Shading { get; }

    /// <summary>
    /// Gets the optional extended graphics state dictionary (may be null).
    /// </summary>
    public PdfDictionary ExtGState { get; } // TODO: [LOW] use

    internal override void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        var matrix = SKMatrix.Concat(state.CTM.Invert(), PatternMatrix);

        var recorder = new PdfCommandRecorder();

        recorder.Process(new SaveStateCommand());
        recorder.Process(new ConcatMatrixCommand(matrix));

        if (Shading.BBox.HasValue)
        {
            recorder.Process(new ClipPathCommand(Shading.BBox.Value, SKClipOperation.Intersect));
        }

        if (Shading.Background != null && Shading.BBox.HasValue)
        {
            var colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(Shading.ColorSpaceConverter);
            var backgroundColor = colorSpace.ToSrgb(Shading.Background, state.RenderingIntent, state.FullTransferFunction);
            var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            using var rectPath = new SKPath();
            rectPath.AddRect(Shading.BBox.Value);

            recorder.Process(new DrawPathCommand(rectPath, backgroundPaint));
        }

        var context = new ShadingDecodingContext(state, Shading);
        recorder.Process(new PdfDrawShadingCommand(Shading, context, state.Page.Document.LoggerFactory));

        recorder.Process(new RestoreStateCommand());

        renderTarget.BeforePatternRender(processor);
        processor.Process(new DrawRecordingCommand(recorder));
        renderTarget.AfterPatternRender(processor);
    }
}
