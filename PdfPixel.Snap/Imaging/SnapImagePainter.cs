using PdfPixel.Color.Paint;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// Paints a row of an image, its soft mask as the mask and its color key as its shape; a tint replaces its color when
/// the paint says so.
/// </summary>
internal sealed class SnapImagePainter : ISnapRowPainter, IDisposable
{
    private readonly PdfSnapSurface _surface;
    private readonly SnapImagePlacement _colors;
    private readonly int _imageComponentCount;
    private readonly bool _hasColorKey;
    private readonly SnapImagePlacement? _mask;
    private readonly float[]? _matte;
    private readonly SnapColorConverter _converter;
    private readonly PdfPaint _paint;
    private readonly float[] _paintColor;
    private readonly float _paintAlpha;
    private readonly bool _takesPaintColor;
    private readonly float[] _colorValues;
    private readonly float[] _maskValues;
    private readonly float[] _unblended;

    /// <summary>
    /// Initializes a painter; <paramref name="colors"/> holds the image's components, followed by the color key's
    /// shape when it has one.
    /// </summary>
    public SnapImagePainter(
        PdfSnapSurface surface,
        SnapImagePlacement colors,
        int imageComponentCount,
        SnapImagePlacement? mask,
        float[]? matte,
        SnapColorConverter converter,
        PdfPaint paint,
        float[] paintColor,
        float paintAlpha,
        bool takesPaintColor,
        int width)
    {
        _surface = surface;
        _colors = colors;
        _imageComponentCount = imageComponentCount;
        _hasColorKey = colors.ChannelCount > imageComponentCount;
        _mask = mask;
        _matte = matte;
        _converter = converter;
        _paint = paint;
        _paintColor = paintColor;
        _paintAlpha = paintAlpha;
        _takesPaintColor = takesPaintColor;
        _colorValues = new float[width * colors.ChannelCount];
        _maskValues = new float[width];
        _unblended = new float[imageComponentCount];
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void PaintRow(int y, int left, Span<float> shapes)
    {
        int count = shapes.Length;
        int channelCount = _colors.ChannelCount;
        int componentCount = _surface.ComponentCount;
        Span<float> values = _colorValues.AsSpan(0, count * channelCount);
        Span<float> maskValues = _maskValues.AsSpan(0, count);
        Span<float> colors = _surface.GetRowColors(count);

        _colors.ReadRow(y, left, values);
        _mask?.ReadRow(y, left, maskValues);

        var colorByteCount = (uint)(componentCount * sizeof(float));
        ref float shape = ref MemoryMarshal.GetReference(shapes);
        ref float value = ref MemoryMarshal.GetReference(values);
        ref float maskValue = ref MemoryMarshal.GetReference(maskValues);
        ref float color = ref MemoryMarshal.GetReference(colors);
        ref float convertedValue = ref value;
        ref float convertedColor = ref color;
        var hasConvertedColor = false;

        for (int index = 0; index < count; index++)
        {
            float pixelShape = shape;

            if (pixelShape > 0)
            {
                if (_hasColorKey)
                {
                    pixelShape *= Unsafe.Add(ref value, _imageComponentCount);
                }

                shape = pixelShape;

                if (pixelShape > 0 && !_takesPaintColor)
                {
                    if (_matte != null)
                    {
                        Unblend(ref value, maskValue);
                        _converter.Convert(ref _unblended[0], ref color);
                    }
                    else if (hasConvertedColor && SnapComponents.AreEqual(ref value, ref convertedValue, _imageComponentCount))
                    {
                        Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref color), ref Unsafe.As<float, byte>(ref convertedColor), colorByteCount);
                    }
                    else
                    {
                        _converter.Convert(ref value, ref color);
                        convertedValue = ref value;
                        convertedColor = ref color;
                        hasConvertedColor = true;
                    }
                }
            }

            shape = ref Unsafe.Add(ref shape, 1);
            value = ref Unsafe.Add(ref value, channelCount);
            maskValue = ref Unsafe.Add(ref maskValue, 1);
            color = ref Unsafe.Add(ref color, componentCount);
        }

        SnapRowValues colorValues;

        if (_takesPaintColor)
        {
            colorValues = new SnapRowValues(_paintColor, true);
        }
        else
        {
            colorValues = new SnapRowValues(colors, false);
        }

        if (_mask != null)
        {
            _surface.PaintRow(y, left, shapes, colorValues, SnapRowValues.One, _paintAlpha, _paint, new SnapRowValues(maskValues, false));
        }
        else
        {
            _surface.PaintRow(y, left, shapes, colorValues, SnapRowValues.One, _paintAlpha, _paint);
        }
    }

    /// <summary>
    /// Recovers colors preblended with the matte (ISO 32000-2 11.6.5.2): c = m + (c' - m) / a.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void Unblend(ref float components, float alpha)
    {
        if (_matte == null)
        {
            return;
        }

        // TODO: [MEDIUM] Clamp the recovered components to the range of the image's color space (ISO 32000-2 11.6.5.2).
        float inverseAlpha = (alpha > 0) ? 1 / alpha : 0;

        for (int component = 0; component < _imageComponentCount; component++)
        {
            float matte = _matte[component];
            _unblended[component] = matte + ((Unsafe.Add(ref components, component) - matte) * inverseAlpha);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _colors.Dispose();
        _mask?.Dispose();
    }
}
