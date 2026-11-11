using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Rendering;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Color;

/// <summary>
/// Identifies a <see cref="SnapColorConverter"/>, comparing color spaces and transfer function by reference.
/// </summary>
internal readonly struct SnapColorConverterKey : IEquatable<SnapColorConverterKey>
{
    public SnapColorConverterKey(
        PdfColorSpaceConverter sourceColorSpace,
        PdfDeviceColorMapping deviceColorMapping,
        PdfRenderingIntent renderingIntent,
        bool useBlackPointCompensation,
        PdfTransferFunction? transferFunction,
        PdfColorSpaceConverter targetColorSpace)
    {
        SourceColorSpace = sourceColorSpace;
        DeviceColorMapping = deviceColorMapping;
        RenderingIntent = renderingIntent;
        UseBlackPointCompensation = useBlackPointCompensation;
        TransferFunction = transferFunction;
        TargetColorSpace = targetColorSpace;
    }

    public PdfColorSpaceConverter SourceColorSpace { get; }

    /// <summary>
    /// Gets the color spaces device colors are interpreted in.
    /// </summary>
    public PdfDeviceColorMapping DeviceColorMapping { get; }

    public PdfRenderingIntent RenderingIntent { get; }

    public bool UseBlackPointCompensation { get; }

    public PdfTransferFunction? TransferFunction { get; }

    public PdfColorSpaceConverter TargetColorSpace { get; }

    /// <inheritdoc />
    public bool Equals(SnapColorConverterKey other)
    {
        return ReferenceEquals(SourceColorSpace, other.SourceColorSpace)
            && DeviceColorMapping.Equals(other.DeviceColorMapping)
            && RenderingIntent == other.RenderingIntent
            && UseBlackPointCompensation == other.UseBlackPointCompensation
            && ReferenceEquals(TransferFunction, other.TransferFunction)
            && ReferenceEquals(TargetColorSpace, other.TargetColorSpace);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SnapColorConverterKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            RuntimeHelpers.GetHashCode(SourceColorSpace),
            DeviceColorMapping,
            RenderingIntent,
            UseBlackPointCompensation,
            RuntimeHelpers.GetHashCode(TransferFunction),
            RuntimeHelpers.GetHashCode(TargetColorSpace));
    }
}
