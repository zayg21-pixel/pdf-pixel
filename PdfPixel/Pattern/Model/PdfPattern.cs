using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Pattern.Model;

/// <summary>
/// Base class for all PDF pattern types (/Pattern). Provides common metadata shared by
/// tiling and shading patterns.
/// </summary>
public abstract class PdfPattern
{
    protected PdfPattern(PdfObject sourceObject, SKMatrix matrix, PdfPatternType patternType)
    {
        SourceObject = sourceObject;
        PatternMatrix = matrix;
        PatternType = patternType;
    }

    /// <summary>
    /// Original source PDF object for the pattern.
    /// </summary>
    public PdfObject SourceObject { get; }

    /// <summary>
    /// Pattern transformation matrix (identity if /Matrix absent).
    /// </summary>
    public SKMatrix PatternMatrix { get; }

    /// <summary>
    /// Underlying pattern type enum.
    /// </summary>
    public PdfPatternType PatternType { get; }

    /// <summary>
    /// Renders the pattern using the given command processor and graphics state.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    /// <param name="state">Actual graphics state.</param>
    /// <param name="renderTarget">Pattern render target.</param>
    internal abstract void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget);
}
