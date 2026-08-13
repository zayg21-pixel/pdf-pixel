using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The grid-fitting and smoothing behavior a "gasp" range asks the rasterizer for. The symmetric
/// flags are defined by version 1 of the table only; a version 0 table carries just the first two.
/// </summary>
[Flags]
public enum SfntGaspBehavior
{
    /// <summary>
    /// Neither grid-fit nor anti-alias glyphs in this range.
    /// </summary>
    None = 0,

    /// <summary>
    /// Grid-fit (hint) glyphs in this range.
    /// </summary>
    GridFit = 1 << 0,

    /// <summary>
    /// Anti-alias glyphs in this range with grayscale smoothing.
    /// </summary>
    DoGray = 1 << 1,

    /// <summary>
    /// Grid-fit glyphs in this range when rendering with symmetric (subpixel) smoothing. Version 1 only.
    /// </summary>
    SymmetricGridFit = 1 << 2,

    /// <summary>
    /// Anti-alias glyphs in this range with symmetric (subpixel) smoothing. Version 1 only.
    /// </summary>
    SymmetricSmoothing = 1 << 3
}
