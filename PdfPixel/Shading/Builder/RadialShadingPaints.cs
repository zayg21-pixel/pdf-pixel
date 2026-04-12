using SkiaSharp;
using System;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the inner and outer paints produced by radial (Type 3) shading.
/// Disposes both paints when the instance is disposed.
/// </summary>
public sealed class RadialShadingPaints : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RadialShadingPaints"/> class.
    /// </summary>
    /// <param name="innerPaint">Paint for the inner cone.</param>
    /// <param name="outerPaint">Paint for the outer cone.</param>
    public RadialShadingPaints(SKPaint innerPaint, SKPaint outerPaint)
    {
        InnerPaint = innerPaint;
        OuterPaint = outerPaint;
    }

    /// <summary>
    /// Gets the paint for the inner cone of the radial gradient.
    /// </summary>
    public SKPaint InnerPaint { get; }

    /// <summary>
    /// Gets the paint for the outer cone of the radial gradient.
    /// </summary>
    public SKPaint OuterPaint { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        InnerPaint?.Dispose();
        OuterPaint?.Dispose();
    }
}
