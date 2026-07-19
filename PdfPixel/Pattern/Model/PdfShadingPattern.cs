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
    /// <param name="sourceObject">The original source PDF object for the pattern.</param>
    /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
    /// <param name="matrix">The pattern transformation matrix.</param>
    /// <param name="extGState">Optional extended graphics state dictionary (may be null).</param>
    internal PdfShadingPattern(
        PdfObject sourceObject,
        PdfShading shading,
        in PdfMatrix matrix,
        PdfDictionary? extGState)
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
    public PdfDictionary? ExtGState { get; } // TODO: [LOW] implement support for ExtGState as per specification. Though, I could never find PDF with ExtGState in shading

    internal override void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        PdfMatrix matrix = PdfMatrix.Concat(state.CTM.Invert(), PatternMatrix);

        PdfCommandRecorder recorder = new(state.Page.Document.LoggerFactory.CreateLogger<PdfCommandRecorder>());

        recorder.Process(SaveStateCommand.Instance);
        recorder.Process(new ConcatMatrixCommand(matrix));

        if (Shading.BBox.HasValue)
        {
            recorder.Process(new ClipRectangleCommand(Shading.BBox.Value, PdfClipOperation.Intersect));
        }

        if (Shading.Background != null && Shading.BBox.HasValue)
        {
            PdfColorSpaceConverter? colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(Shading.ColorSpaceObject) ?? DeviceRgbConverter.Instance;
            PdfColor backgroundColor = colorSpace.ToSrgb(Shading.Background, state.RenderingIntent, state.FullTransferFunction);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            using SKPathBuilder rectPathBuilder = new();
            rectPathBuilder.AddRect(Shading.BBox.Value.ToSkRect());

            recorder.Process(new DrawPathCommand(rectPathBuilder.Detach(), backgroundPaint));
        }

        ShadingDecodingContext context = new(state, Shading);
        recorder.Process(new DrawShadingCommand(context, state.Page.Document.LoggerFactory));

        recorder.Process(RestoreStateCommand.Instance);

        renderTarget.BeforePatternRender(processor);
        processor.Process(new DrawRecordingCommand(recorder));
        renderTarget.AfterPatternRender(processor);
    }
}
