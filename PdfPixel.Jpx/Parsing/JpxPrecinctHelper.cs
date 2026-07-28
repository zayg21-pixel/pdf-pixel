using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Helper class for calculating precinct dimensions and coordinates in JPEG 2000 (JPX) images.
/// Handles precinct grid computation based on coding style parameters and resolution levels.
/// </summary>
internal static class JpxPrecinctHelper
{
    /// <summary>
    /// Default precinct size exponent used when the coding style specifies no precinct sizes.
    /// </summary>
    private const int DefaultPrecinctExponent = 15;

    /// <summary>
    /// Reads the base-2 exponents of the precinct dimensions for a given resolution level.
    /// </summary>
    /// <param name="resolutionLevel">The resolution level (0 = lowest resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Tuple containing the precinct width and height exponents.</returns>
    public static (int widthExponent, int heightExponent) GetPrecinctExponents(
        int resolutionLevel,
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle? componentCodingStyle = null)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        // Use component-specific coding style if available
        JpxCodingStyle effectiveCodingStyle = componentCodingStyle?.CodingStyle ?? codingStyle;

        if (effectiveCodingStyle.HasPrecinctSizes
            && resolutionLevel < effectiveCodingStyle.PrecinctSizeExponents.Length)
        {
            byte sizeExponents = effectiveCodingStyle.PrecinctSizeExponents[resolutionLevel];

            // ITU-T T.800 Table A.21: bits 0-3 = PPx (width), bits 4-7 = PPy (height)
            return (sizeExponents & 0x0F, (sizeExponents >> 4) & 0x0F);
        }

        return (DefaultPrecinctExponent, DefaultPrecinctExponent);
    }

    /// <summary>
    /// Computes the precinct dimensions for a given resolution level.
    /// </summary>
    /// <param name="resolutionLevel">The resolution level (0 = lowest resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Tuple containing precinct width and height in pixels.</returns>
    public static (int width, int height) GetPrecinctSize(
        int resolutionLevel,
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle? componentCodingStyle = null)
    {
        (int widthExponent, int heightExponent) = GetPrecinctExponents(resolutionLevel, codingStyle, componentCodingStyle);

        return (1 << widthExponent, 1 << heightExponent);
    }

    /// <summary>
    /// Computes the number of precincts for a given tile and resolution level.
    /// </summary>
    /// <param name="tileBounds">Bounds of the tile on the reference grid.</param>
    /// <param name="resolutionLevel">The resolution level (0 = full resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Tuple containing number of precincts horizontally and vertically.</returns>
    public static (int precinctsX, int precinctsY) ComputePrecinctGrid(
        in JpxRectangle tileBounds,
        int resolutionLevel,
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle? componentCodingStyle = null)
    {
        if (tileBounds.Width <= 0 || tileBounds.Height <= 0)
        {
            return (0, 0);
        }

        (int precinctWidth, int precinctHeight) = GetPrecinctSize(resolutionLevel, codingStyle, componentCodingStyle);

        // Precincts partition the resolution grid from its origin, so the count spans every
        // partition cell the tile touches rather than the tile's own width.
        int levels = codingStyle.DecompositionLevels - resolutionLevel;
        int resolutionX0 = JpxBandGeometry.GetResolutionCoordinate(tileBounds.X, levels);
        int resolutionY0 = JpxBandGeometry.GetResolutionCoordinate(tileBounds.Y, levels);
        int resolutionX1 = JpxBandGeometry.GetResolutionCoordinate(tileBounds.Right, levels);
        int resolutionY1 = JpxBandGeometry.GetResolutionCoordinate(tileBounds.Bottom, levels);

        if (resolutionX1 <= resolutionX0 || resolutionY1 <= resolutionY0)
        {
            return (0, 0);
        }

        int precinctsX = CeilDivide(resolutionX1, precinctWidth) - (resolutionX0 / precinctWidth);
        int precinctsY = CeilDivide(resolutionY1, precinctHeight) - (resolutionY0 / precinctHeight);

        return (Math.Max(precinctsX, 1), Math.Max(precinctsY, 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilDivide(int value, int divisor) => (value + divisor - 1) / divisor;
}
