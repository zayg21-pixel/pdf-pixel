using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Helper class for calculating precinct dimensions and coordinates in JPEG 2000 (JPX) images.
/// Handles precinct grid computation based on coding style parameters and resolution levels.
/// </summary>
internal static class JpxPrecinctHelper
{
    /// <summary>
    /// Computes the precinct dimensions for a given resolution level.
    /// </summary>
    /// <param name="resolutionLevel">The resolution level (0 = full resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Tuple containing precinct width and height in pixels.</returns>
    public static (int width, int height) GetPrecinctSize(
        int resolutionLevel, 
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle componentCodingStyle = null)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        // Use component-specific coding style if available
        var effectiveCodingStyle = componentCodingStyle?.CodingStyle ?? codingStyle;

        // Check if precinct sizes are explicitly specified in the coding style
        if (effectiveCodingStyle.HasPrecinctSizes && 
            resolutionLevel < effectiveCodingStyle.PrecinctSizeExponents.Length)
        {
            byte sizeExponents = effectiveCodingStyle.PrecinctSizeExponents[resolutionLevel];
            
            // Extract width and height exponents from the byte
            // ITU-T T.800 Table A.21: bits 0-3 = PPx (width), bits 4-7 = PPy (height)
            int widthExponent = sizeExponents & 0x0F;
            int heightExponent = (sizeExponents >> 4) & 0x0F;
            
            // Calculate actual precinct dimensions: 2^exponent
            int width = 1 << widthExponent;
            int height = 1 << heightExponent;
            
            return (width, height);
        }

        // Default behavior when no explicit precinct sizes are specified
        // JPEG2000 standard: default precinct size is 2^15 (32768) for all resolution levels
        return (1 << 15, 1 << 15);
    }

    /// <summary>
    /// Computes the number of precincts for a given tile and resolution level.
    /// </summary>
    /// <param name="tileWidth">Width of the tile in pixels.</param>
    /// <param name="tileHeight">Height of the tile in pixels.</param>
    /// <param name="resolutionLevel">The resolution level (0 = full resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Tuple containing number of precincts horizontally and vertically.</returns>
    public static (int precinctsX, int precinctsY) ComputePrecinctGrid(
        int tileWidth,
        int tileHeight,
        int resolutionLevel,
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle componentCodingStyle = null)
    {
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return (0, 0);
        }

        var (precinctWidth, precinctHeight) = GetPrecinctSize(resolutionLevel, codingStyle, componentCodingStyle);

        // Compute resolution-dependent tile dimensions using ITU-T T.800 formula:
        // trx1 - trx0 = ceil(tileWidth / 2^(NL - r)) for resolution r
        int decompositionLevels = codingStyle.DecompositionLevels;
        int shift = decompositionLevels - resolutionLevel;
        int resolutionTileWidth = (shift <= 0) ? tileWidth : (tileWidth + (1 << shift) - 1) >> shift;
        int resolutionTileHeight = (shift <= 0) ? tileHeight : (tileHeight + (1 << shift) - 1) >> shift;

        resolutionTileWidth = Math.Max(1, resolutionTileWidth);
        resolutionTileHeight = Math.Max(1, resolutionTileHeight);

        // Calculate number of precincts needed to cover the tile at this resolution
        int precinctsX = (resolutionTileWidth + precinctWidth - 1) / precinctWidth;
        int precinctsY = (resolutionTileHeight + precinctHeight - 1) / precinctHeight;

        return (precinctsX, precinctsY);
    }
}