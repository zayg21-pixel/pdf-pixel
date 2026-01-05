using PdfReader.Imaging.Jpx.Model;
using System;

namespace PdfReader.Imaging.Jpx.Parsing;

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
            int widthExponent = (sizeExponents >> 4) & 0x0F;
            int heightExponent = sizeExponents & 0x0F;
            
            // Calculate actual precinct dimensions: 2^exponent
            int width = 1 << widthExponent;
            int height = 1 << heightExponent;
            
            return (width, height);
        }

        // Default behavior when no explicit precinct sizes are specified
        // JPEG2000 standard requires default precinct size of 2^15 (32768) for all resolution levels
        // However, this creates massive precincts that can cause memory/performance issues
        
        // COMPROMISE APPROACH: Use spec-compliant defaults but with practical limits
        int specDefaultSize = 1 << 15; // 32768 - JPEG2000 specification requirement
        
        // Apply resolution scaling - at higher decomposition levels, precincts get smaller  
        int precinctWidth = specDefaultSize >> resolutionLevel;
        int precinctHeight = specDefaultSize >> resolutionLevel;
        
        // Clamp to practical minimums while maintaining spec compliance intent
        precinctWidth = Math.Max(precinctWidth, 64);
        precinctHeight = Math.Max(precinctHeight, 64);
        
        // Note: At resolution 0 (full res), this gives 32768x32768 as required by spec
        // At higher resolutions, it scales down: res 1 = 16384x16384, res 2 = 8192x8192, etc.
        return (precinctWidth, precinctHeight);
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

        // Compute resolution-dependent tile dimensions
        int resolutionTileWidth = Math.Max(1, tileWidth >> resolutionLevel);
        int resolutionTileHeight = Math.Max(1, tileHeight >> resolutionLevel);

        // Calculate number of precincts needed to cover the tile at this resolution
        int precinctsX = (resolutionTileWidth + precinctWidth - 1) / precinctWidth;
        int precinctsY = (resolutionTileHeight + precinctHeight - 1) / precinctHeight;

        return (precinctsX, precinctsY);
    }

    /// <summary>
    /// Computes the bounds of a specific precinct within a tile.
    /// </summary>
    /// <param name="precinctX">Precinct X coordinate (0-based).</param>
    /// <param name="precinctY">Precinct Y coordinate (0-based).</param>
    /// <param name="tileWidth">Width of the tile in pixels.</param>
    /// <param name="tileHeight">Height of the tile in pixels.</param>
    /// <param name="resolutionLevel">The resolution level (0 = full resolution).</param>
    /// <param name="codingStyle">Coding style parameters from the main header.</param>
    /// <param name="componentCodingStyle">Component-specific coding style overrides (optional).</param>
    /// <returns>Rectangle defining the precinct bounds in tile coordinates.</returns>
    public static (int x, int y, int width, int height) GetPrecinctBounds(
        int precinctX,
        int precinctY,
        int tileWidth,
        int tileHeight,
        int resolutionLevel,
        JpxCodingStyle codingStyle,
        JpxComponentCodingStyle componentCodingStyle = null)
    {
        var (precinctWidth, precinctHeight) = GetPrecinctSize(resolutionLevel, codingStyle, componentCodingStyle);

        // Compute resolution-dependent tile dimensions
        int resolutionTileWidth = Math.Max(1, tileWidth >> resolutionLevel);
        int resolutionTileHeight = Math.Max(1, tileHeight >> resolutionLevel);

        // Calculate precinct position and size
        int x = precinctX * precinctWidth;
        int y = precinctY * precinctHeight;
        
        // Clamp to tile boundaries
        int actualWidth = Math.Min(precinctWidth, resolutionTileWidth - x);
        int actualHeight = Math.Min(precinctHeight, resolutionTileHeight - y);

        // Ensure positive dimensions
        actualWidth = Math.Max(0, actualWidth);
        actualHeight = Math.Max(0, actualHeight);

        return (x, y, actualWidth, actualHeight);
    }

    /// <summary>
    /// Computes the number of code blocks within a precinct.
    /// </summary>
    /// <param name="precinctWidth">Width of the precinct in pixels.</param>
    /// <param name="precinctHeight">Height of the precinct in pixels.</param>
    /// <param name="codingStyle">Coding style parameters containing code block size.</param>
    /// <returns>Tuple containing number of code blocks horizontally and vertically.</returns>
    public static (int codeBlocksX, int codeBlocksY) GetCodeBlockGrid(
        int precinctWidth,
        int precinctHeight,
        JpxCodingStyle codingStyle)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        int codeBlockWidth = codingStyle.CodeBlockWidth;
        int codeBlockHeight = codingStyle.CodeBlockHeight;

        if (codeBlockWidth <= 0 || codeBlockHeight <= 0)
        {
            return (0, 0);
        }

        int codeBlocksX = (precinctWidth + codeBlockWidth - 1) / codeBlockWidth;
        int codeBlocksY = (precinctHeight + codeBlockHeight - 1) / codeBlockHeight;

        return (codeBlocksX, codeBlocksY);
    }

    /// <summary>
    /// Validates that the precinct coordinates are within the expected grid bounds.
    /// </summary>
    /// <param name="precinctX">Precinct X coordinate to validate.</param>
    /// <param name="precinctY">Precinct Y coordinate to validate.</param>
    /// <param name="maxPrecinctsX">Maximum number of precincts horizontally.</param>
    /// <param name="maxPrecinctsY">Maximum number of precincts vertically.</param>
    /// <returns>True if coordinates are valid.</returns>
    public static bool IsValidPrecinctCoordinate(int precinctX, int precinctY, int maxPrecinctsX, int maxPrecinctsY)
    {
        return precinctX >= 0 && precinctX < maxPrecinctsX && 
               precinctY >= 0 && precinctY < maxPrecinctsY;
    }
}