using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using System;

namespace PdfReader.Color.Icc;

/// <summary>
/// Provides analysis capabilities for ICC profiles to detect standard color spaces
/// like sRGB and grayscale profiles based on their TRC curves and color matrices.
/// </summary>
internal static class IccProfileAnalyzer
{
    // Standard sRGB primaries in XYZ (D50 adapted)
    private static readonly IccXyz StandardSrgbRedPrimary = new IccXyz(0.4361f, 0.2225f, 0.0139f);
    private static readonly IccXyz StandardSrgbGreenPrimary = new IccXyz(0.3851f, 0.7169f, 0.0971f);
    private static readonly IccXyz StandardSrgbBluePrimary = new IccXyz(0.1431f, 0.0606f, 0.7141f);

    // Standard sRGB gamma approximation (2.2 is close enough for detection)
    private const float StandardSrgbGamma = 2.2f;

    // XYZ component tolerance for matrix comparison
    private const float XyzTolerance = 0.01f;

    // TRC comparison parameters
    private const int TrcComparisonPoints = 32;
    private const float TrcTolerance = 0.02f; // 2% tolerance for TRC comparison

    /// <summary>
    /// Determines whether the specified ICC profile represents a standard sRGB color space.
    /// This checks for standard sRGB primaries and gamma curves without LUT-based transforms.
    /// </summary>
    /// <param name="profile">The ICC profile to analyze.</param>
    /// <returns>true if the profile represents standard sRGB; otherwise, false.</returns>
    public static bool IsStandardSrgb(IccProfile profile)
    {
        if (profile == null)
        {
            return false;
        }

        // Must be RGB (3 channels)
        if (profile.ChannelsCount != 3)
        {
            return false;
        }

        // Must not be LUT-based (check for A2B LUTs)
        if (HasA2BLuts(profile))
        {
            return false;
        }

        // Must have matrix/TRC structure
        if (!profile.RedMatrix.HasValue || !profile.GreenMatrix.HasValue || !profile.BlueMatrix.HasValue)
        {
            return false;
        }

        if (profile.RedTrc == null || profile.GreenTrc == null || profile.BlueTrc == null)
        {
            return false;
        }

        // Check RGB primaries match standard sRGB (within tolerance)
        if (!IsXyzClose(profile.RedMatrix.Value, StandardSrgbRedPrimary, XyzTolerance))
        {
            return false;
        }

        if (!IsXyzClose(profile.GreenMatrix.Value, StandardSrgbGreenPrimary, XyzTolerance))
        {
            return false;
        }

        if (!IsXyzClose(profile.BlueMatrix.Value, StandardSrgbBluePrimary, XyzTolerance))
        {
            return false;
        }

        // Create reference sRGB gamma curve for comparison
        var referenceTrc = IccTrc.FromGamma(StandardSrgbGamma);

        // Check TRC curves are standard sRGB-like using 32-point analysis
        return IsTrcSimilar(profile.RedTrc, referenceTrc, TrcComparisonPoints, TrcTolerance) &&
               IsTrcSimilar(profile.GreenTrc, referenceTrc, TrcComparisonPoints, TrcTolerance) &&
               IsTrcSimilar(profile.BlueTrc, referenceTrc, TrcComparisonPoints, TrcTolerance);
    }

    /// <summary>
    /// Determines whether the specified ICC profile represents a standard grayscale color space.
    /// This checks for a simple gamma curve without LUT-based transforms.
    /// </summary>
    /// <param name="profile">The ICC profile to analyze.</param>
    /// <returns>true if the profile represents standard grayscale; otherwise, false.</returns>
    public static bool IsStandardGray(IccProfile profile)
    {
        if (profile == null)
        {
            return false;
        }

        // Must be grayscale (1 channel)
        if (profile.ChannelsCount != 1)
        {
            return false;
        }

        // Must not be LUT-based
        if (HasA2BLuts(profile))
        {
            return false;
        }

        // Must have gray TRC
        if (profile.GrayTrc == null)
        {
            return false;
        }

        // Create reference gamma curve for comparison
        var referenceTrc = IccTrc.FromGamma(StandardSrgbGamma);

        // Check if gray TRC is similar to standard gamma using 32-point analysis
        return IsTrcSimilar(profile.GrayTrc, referenceTrc, TrcComparisonPoints, TrcTolerance);
    }

    /// <summary>
    /// Compares two TRC curves by evaluating them at multiple points and checking if they are similar within tolerance.
    /// This approach works for all TRC types: gamma, sampled, and parametric curves.
    /// </summary>
    /// <param name="trc1">The first TRC to compare.</param>
    /// <param name="trc2">The second TRC to compare.</param>
    /// <param name="points">Number of points to evaluate for comparison.</param>
    /// <param name="tolerance">Maximum difference allowed at each point.</param>
    /// <returns>true if the TRCs are similar within tolerance; otherwise, false.</returns>
    private static bool IsTrcSimilar(IccTrc trc1, IccTrc trc2, int points, float tolerance)
    {
        if (trc1 == null || trc2 == null)
        {
            return trc1 == trc2; // Both null is considered equal
        }

        // Compare TRCs by evaluating at evenly spaced points
        for (int i = 0; i < points; i++)
        {
            float x = i / (float)(points - 1);
            float value1 = IccTrcEvaluator.EvaluateTrc(trc1, x);
            float value2 = IccTrcEvaluator.EvaluateTrc(trc2, x);

            if (Math.Abs(value1 - value2) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the profile has any A2B LUT tables, indicating it's LUT-based rather than matrix/TRC-based.
    /// </summary>
    /// <param name="profile">The ICC profile to check.</param>
    /// <returns>true if the profile has A2B LUTs; otherwise, false.</returns>
    private static bool HasA2BLuts(IccProfile profile)
    {
        return profile.A2BLut0 != null || profile.A2BLut1 != null || profile.A2BLut2 != null;
    }

    /// <summary>
    /// Checks if two XYZ values are close within the specified tolerance.
    /// </summary>
    /// <param name="a">The first XYZ value.</param>
    /// <param name="b">The second XYZ value.</param>
    /// <param name="tolerance">The tolerance for comparison.</param>
    /// <returns>true if the values are within tolerance; otherwise, false.</returns>
    private static bool IsXyzClose(IccXyz a, IccXyz b, float tolerance)
    {
        return Math.Abs(a.X - b.X) <= tolerance &&
               Math.Abs(a.Y - b.Y) <= tolerance &&
               Math.Abs(a.Z - b.Z) <= tolerance;
    }
}