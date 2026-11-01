using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands.Cache;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia.Cache;

internal sealed class SkiaColorSamplerKey : ICommandCacheKey
{
    private readonly PdfColorSpaceConverter _colorSpace;
    private readonly PdfDeviceColorMapping _deviceColorMapping;
    private readonly PdfRenderingIntent _renderingIntent;
    private readonly bool _useBlackPointCompensation;
    private readonly TransferFunctionTransform? _transferFunction;

    public SkiaColorSamplerKey(PdfPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        _colorSpace = paint.ColorSpace;
        _deviceColorMapping = paint.DeviceColorMapping;
        _renderingIntent = paint.RenderingIntent;
        _useBlackPointCompensation = paint.UseBlackPointCompensation;
        _transferFunction = paint.TransferFunction;
    }

    public bool Equals(ICommandCacheKey? other)
    {
        return other is SkiaColorSamplerKey key
            && ReferenceEquals(_colorSpace, key._colorSpace)
            && _deviceColorMapping.Equals(key._deviceColorMapping)
            && _renderingIntent == key._renderingIntent
            && _useBlackPointCompensation == key._useBlackPointCompensation
            && ReferenceEquals(_transferFunction, key._transferFunction);
    }

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode()
        => HashCode.Combine(RuntimeHelpers.GetHashCode(_colorSpace), _deviceColorMapping, _renderingIntent, _useBlackPointCompensation, RuntimeHelpers.GetHashCode(_transferFunction));
}
