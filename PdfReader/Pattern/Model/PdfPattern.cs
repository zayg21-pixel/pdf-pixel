using PdfReader.Models;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using SkiaSharp;
using System;

namespace PdfReader.Pattern.Model;

public enum PdfPatternType
{
    Tiling = 1,
    Shading = 2
}

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

    /// <summary>Original source PDF object for the pattern.</summary>
    public PdfObject SourceObject { get; }

    /// <summary>Pattern transformation matrix (identity if /Matrix absent).</summary>
    public SKMatrix PatternMatrix { get; }

    /// <summary>Underlying pattern type enum.</summary>
    public PdfPatternType PatternType { get; }

    /// <summary>
    /// Renders the pattern onto the provided canvas using the given graphics state.
    /// </summary>
    /// <param name="canvas">Current canvas with CTM applied.</param>
    /// <param name="state">Actual graphics state.</param>
    /// <param name="renderTarget">Pattern render target.</param>
    internal abstract void RenderPattern(SKCanvas canvas, PdfGraphicsState state, IRenderTarget renderTarget);
}
