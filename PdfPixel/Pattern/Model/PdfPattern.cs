using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;

namespace PdfPixel.Pattern.Model;

/// <summary>
/// Base class for all PDF pattern types (/Pattern). Provides common metadata shared by
/// tiling and shading patterns.
/// </summary>
public abstract class PdfPattern
{
    /// <summary>
    /// Initializes the pattern with its transformation matrix and pattern type.
    /// </summary>
    protected PdfPattern(in PdfMatrix matrix, PdfPatternType patternType)
    {
        PatternMatrix = matrix;
        PatternType = patternType;
    }

    /// <summary>
    /// Pattern transformation matrix (identity if /Matrix absent).
    /// </summary>
    public PdfMatrix PatternMatrix { get; }

    /// <summary>
    /// Underlying pattern type enum.
    /// </summary>
    public PdfPatternType PatternType { get; }

    /// <summary>
    /// Whether the pattern holds nothing bound to the page it was parsed from.
    /// </summary>
    internal abstract bool IsPageIndependent { get; }

    /// <summary>
    /// Renders the pattern using the given command processor and graphics state.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    /// <param name="state">Actual graphics state.</param>
    /// <param name="renderTarget">Pattern render target.</param>
    internal abstract void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget);
}
