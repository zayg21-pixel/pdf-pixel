using PdfPixel.Color.Transform;
using PdfPixel.Rendering;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Parameters controlling the conversion of color space values to sRGB.
/// </summary>
public readonly struct PdfColorConversionParameters : IEquatable<PdfColorConversionParameters>
{
    private readonly PdfDeviceColorMapping? _deviceColorMapping;

    /// <summary>
    /// Initializes a new <see cref="PdfColorConversionParameters"/> from its parameters.
    /// </summary>
    internal PdfColorConversionParameters(PdfRenderingIntent renderingIntent, TransferFunctionTransform? transferFunction, bool useBlackPointCompensation)
        : this(renderingIntent, transferFunction, useBlackPointCompensation, PdfDeviceColorMapping.None)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PdfColorConversionParameters"/> from its parameters and the mapping of device color spaces.
    /// </summary>
    internal PdfColorConversionParameters(PdfRenderingIntent renderingIntent, TransferFunctionTransform? transferFunction, bool useBlackPointCompensation, PdfDeviceColorMapping deviceColorMapping)
    {
        RenderingIntent = renderingIntent;
        TransferFunction = transferFunction;
        UseBlackPointCompensation = useBlackPointCompensation;
        _deviceColorMapping = deviceColorMapping;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfColorConversionParameters"/> from a graphics state and the mapping of device color spaces.
    /// </summary>
    internal PdfColorConversionParameters(PdfGraphicsState state, PdfDeviceColorMapping deviceColorMapping)
        : this(state.RenderingIntent, state.TransferFunction, state.UseBlackPointCompensation, deviceColorMapping)
    {
    }

    /// <summary>
    /// Rendering intent.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Transfer function applied after conversion, or <see langword="null"/> when none.
    /// </summary>
    internal TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// Whether black point compensation is applied.
    /// </summary>
    public bool UseBlackPointCompensation { get; }

    /// <summary>
    /// Color spaces device color values are interpreted in.
    /// </summary>
    internal PdfDeviceColorMapping DeviceColorMapping => _deviceColorMapping ?? PdfDeviceColorMapping.None;

    /// <inheritdoc/>
    public bool Equals(PdfColorConversionParameters other)
    {
        return RenderingIntent == other.RenderingIntent
            && ReferenceEquals(TransferFunction, other.TransferFunction)
            && UseBlackPointCompensation == other.UseBlackPointCompensation
            && DeviceColorMapping.Equals(other.DeviceColorMapping);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfColorConversionParameters other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RenderingIntent, RuntimeHelpers.GetHashCode(TransferFunction), UseBlackPointCompensation, DeviceColorMapping);

    /// <summary>
    /// Determines whether two parameter sets are equal.
    /// </summary>
    public static bool operator ==(in PdfColorConversionParameters left, in PdfColorConversionParameters right) => left.Equals(right);

    /// <summary>
    /// Determines whether two parameter sets differ.
    /// </summary>
    public static bool operator !=(in PdfColorConversionParameters left, in PdfColorConversionParameters right) => !left.Equals(right);
}
