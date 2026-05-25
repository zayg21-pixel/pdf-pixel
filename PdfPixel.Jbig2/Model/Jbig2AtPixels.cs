using System;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// A pair of adaptive template (AT) pixel coordinate arrays used in JBIG2 arithmetic coding contexts.
/// Keeps the X and Y offset arrays bound together so they are never passed or stored separately.
/// </summary>
internal readonly struct Jbig2AtPixels
{
    /// <summary>
    /// Initialises the pair with the supplied coordinate arrays.
    /// </summary>
    /// <param name="atX">AT pixel X offsets (one entry per template pixel).</param>
    /// <param name="atY">AT pixel Y offsets (one entry per template pixel).</param>
    public Jbig2AtPixels(sbyte[] atX, sbyte[] atY)
    {
        AtX = atX ?? Array.Empty<sbyte>();
        AtY = atY ?? Array.Empty<sbyte>();
    }

    /// <summary>
    /// AT pixel X offsets.
    /// </summary>
    public sbyte[] AtX { get; }

    /// <summary>
    /// AT pixel Y offsets.
    /// </summary>
    public sbyte[] AtY { get; }

    /// <summary>
    /// Returns <see langword="true"/> when at least one AT pixel coordinate pair is present.
    /// </summary>
    public bool HasValues => AtX.Length > 0;
}
