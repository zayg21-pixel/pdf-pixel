using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Rendering;
using PdfPixel.Shading;
using PdfPixel.Transparency;

namespace PdfPixel.Pattern;

/// <summary>
/// Represents a shading pattern (/PatternType 2) in a PDF document.
/// Provides access to the referenced shading and optional extended graphics state.
/// Caches the base shader for performance.
/// </summary>
public sealed class PdfShadingPattern : PdfPattern
{
    private static readonly PdfTransparencyGroupContext ShadingGroup = new(null, isolated: false, knockout: true);

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

    internal override PdfGroupSource GetGroupSource(PdfGraphicsState state, PdfPaint paint)
    {
        PdfGraphicsState shadingState = state;

        if (ExtGState != null)
        {
            shadingState = state.Clone();
            ExtGState.ApplyToGraphicsState(shadingState);
        }

        PdfMatrix matrix = PdfMatrix.Concat(shadingState.CTM.Invert(), Matrix);
        PdfColorSpaceConverter colorSpace = shadingState.Host.Resources.ResolveColorSpace(Shading.ColorSpaceReference) ?? PdfDeviceRgbColorSpaceConverter.Instance;

        PdfCommandRecorder recorder = new();

        recorder.Process(SaveStateCommand.Instance);
        recorder.Process(new ConcatMatrixCommand(matrix));

        if (Shading.BoundingBox.HasValue)
        {
            PdfPathBuilder clipBuilder = new();
            clipBuilder.AddRect(Shading.BoundingBox.Value);
            recorder.Process(new DrawPathCommand(clipBuilder.ToPath(), new PdfPaint(PdfPaintStyle.Clip)));
        }

        if (Shading.Background != null && Shading.BoundingBox.HasValue)
        {
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(shadingState, colorSpace, Shading.Background);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(Shading.BoundingBox.Value);

            recorder.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        PdfShadingContent content = Shading.GetContent(colorSpace, shadingState);

        PdfColorSpaceConverter contentColorSpace = PdfShadingContent.GetColorSpace(colorSpace);
        PdfPaint shadingPaint = shadingState.FillPaint
            .WithColorSpace(contentColorSpace, shadingState.Host.Resources.DeviceColorMapping)
            .WithAlpha(1f)
            .WithBlendMode(PdfBlendMode.Normal);

        recorder.Process(new DrawShadingCommand(content, shadingPaint, Shading.BoundingBox));

        recorder.Process(RestoreStateCommand.Instance);

        return new PdfGroupSource(new DrawRecordingCommand(recorder), ShadingGroup, null, null);
    }
}
