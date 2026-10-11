using PdfPixel.Color.Transform;
using PdfPixel.Rendering.State;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Parameters controlling the conversion of color space values to sRGB.
/// </summary>
public readonly struct PdfColorConversionParameters : IEquatable<PdfColorConversionParameters>
{
    /// <summary>
    /// Initializes a new <see cref="PdfColorConversionParameters"/> from its parameters.
    /// </summary>
    public PdfColorConversionParameters(PdfRenderingIntent renderingIntent, TransferFunctionTransform? transferFunction, bool useBlackPointCompensation)
    {
        RenderingIntent = renderingIntent;
        TransferFunction = transferFunction;
        UseBlackPointCompensation = useBlackPointCompensation;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfColorConversionParameters"/> from a graphics state.
    /// </summary>
    internal PdfColorConversionParameters(PdfGraphicsState state)
    {
        RenderingIntent = state.RenderingIntent;
        TransferFunction = state.TransferFunction;
        UseBlackPointCompensation = state.UseBlackPointCompensation;
    }

    /// <summary>
    /// Rendering intent.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Transfer function applied after conversion, or <see langword="null"/> when none.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// Whether black point compensation is applied.
    /// </summary>
    public bool UseBlackPointCompensation { get; }

    /// <inheritdoc/>
    public bool Equals(PdfColorConversionParameters other)
    {
        return RenderingIntent == other.RenderingIntent
            && ReferenceEquals(TransferFunction, other.TransferFunction)
            && UseBlackPointCompensation == other.UseBlackPointCompensation;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfColorConversionParameters other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RenderingIntent, RuntimeHelpers.GetHashCode(TransferFunction), UseBlackPointCompensation);

    /// <summary>
    /// Determines whether two parameter sets are equal.
    /// </summary>
    public static bool operator ==(in PdfColorConversionParameters left, in PdfColorConversionParameters right) => left.Equals(right);

    /// <summary>
    /// Determines whether two parameter sets differ.
    /// </summary>
    public static bool operator !=(in PdfColorConversionParameters left, in PdfColorConversionParameters right) => !left.Equals(right);
}
