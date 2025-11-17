using PdfReader.Color.ColorSpace;
using PdfReader.Models;
using PdfReader.Rendering.State;
using PdfReader.Shading;
using PdfReader.Shading.Model;
using SkiaSharp;

namespace PdfReader.Pattern.Model;

/// <summary>
/// Represents a shading pattern (/PatternType 2) in a PDF document.
/// Provides access to the referenced shading and optional extended graphics state.
/// Caches the base shader for performance.
/// </summary>
public sealed class PdfShadingPattern : PdfPattern
{
    private SKPicture _cachedBasePicture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingPattern"/> class with the specified parameters.
    /// </summary>
    /// <param name="page">The owning PDF page context.</param>
    /// <param name="sourceObject">The original source PDF object for the pattern.</param>
    /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
    /// <param name="matrix">The pattern transformation matrix.</param>
    /// <param name="extGState">Optional extended graphics state dictionary (may be null).</param>
    internal PdfShadingPattern(
        PdfPage page,
        PdfObject sourceObject,
        PdfShading shading,
        SKMatrix matrix,
        PdfDictionary extGState)
        : base(page, sourceObject, matrix, PdfPatternType.Shading)
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
    public PdfDictionary ExtGState { get; } // TODO: use

    /// <inheritdoc/>
    public override SKPicture AsPicture(PdfGraphicsState state)
    {
        if (_cachedBasePicture != null)
        {
            return _cachedBasePicture;
        }
        _cachedBasePicture = PdfShadingBuilder.ToPicture(Shading);

        return _cachedBasePicture;
    }

    /// <summary>
    /// Disposes the cached shader and releases resources.
    /// </summary>
    public override void Dispose()
    {
        if (_cachedBasePicture != null)
        {
            _cachedBasePicture.Dispose();
            _cachedBasePicture = null;
        }

        base.Dispose();
    }
}
