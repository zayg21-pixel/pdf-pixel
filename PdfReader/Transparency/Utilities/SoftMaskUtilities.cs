using PdfReader.Color.Paint;
using PdfReader.Models;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Model;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Transparency.Utilities;

/// <summary>
/// Utility functions for soft mask processing.
/// Provides helpers to build temporary graphics states and color filters used when interpreting /SMask dictionaries.
/// </summary>
internal static class SoftMaskUtilities
{
    /// <summary>
    /// Create a graphics state optimized for alpha soft mask rendering (Subtype = /Alpha).
    /// We render the mask content in solid white so that the resulting luminance (or direct alpha composition)
    /// produces maximum coverage for painted marks and the per‑object alpha comes only from transparency operators
    /// (e.g. ca/CA) or explicit painting. Using white ensures that stroke/fill operations that do not explicitly
    /// change color contribute a full 1.0 channel and the eventual mask derives only from transparency semantics.
    /// </summary>
    public static PdfGraphicsState CreateAlphaMaskGraphicsState(PdfPage page, HashSet<uint> recursionGuard)
    {
        return new PdfGraphicsState(page, recursionGuard)
        {
            // White stroke/fill -> maximum channel; alpha modulation derives from transparency settings.
            StrokePaint = PdfPaint.Solid(SKColors.White),
            FillPaint = PdfPaint.Solid(SKColors.White),
            StrokeAlpha = 1.0f,
            FillAlpha = 1.0f,
            BlendMode = PdfBlendMode.Normal,
            LineWidth = 1.0f
        };
    }

    /// <summary>
    /// Create a graphics state optimized for luminosity soft mask rendering (Subtype = /Luminosity).
    /// For luminosity masks we keep natural grayscale intent by rendering with black (or dark) base color so that
    /// the mask result comes from actual painted content luminance (after optional color space conversions) rather
    /// than being forced to pure white. This aligns with the PDF spec where a luminosity soft mask derives its
    /// values from the luminance of the group result. Black base simplifies interpretation and avoids unintended
    /// bias toward full alpha when colors are not explicitly set.
    /// </summary>
    public static PdfGraphicsState CreateLuminosityMaskGraphicsState(PdfPage page, HashSet<uint> recursionGuard)
    {
        return new PdfGraphicsState(page, recursionGuard)
        {
            // Black stroke/fill -> preserves true luminance contribution of painted colors.
            StrokePaint = PdfPaint.Solid(SKColors.Black),
            FillPaint = PdfPaint.Solid(SKColors.Black),
            StrokeAlpha = 1.0f,
            FillAlpha = 1.0f,
            BlendMode = PdfBlendMode.Normal,
            LineWidth = 1.0f
        };
    }
}