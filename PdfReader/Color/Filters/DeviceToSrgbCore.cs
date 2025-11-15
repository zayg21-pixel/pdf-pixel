using PdfReader.Color.ColorSpace;
using SkiaSharp;
using System;

namespace PdfReader.Color.Filters;

/// <summary>
/// Common delegate for device-to-sRGB color conversion.
/// </summary>
/// <param name="input">Input color in device color space.</param>
/// <param name="intent">Rendering intent.</param>
/// <returns>Converted color in sRGB color space.</returns>
internal delegate SKColor DeviceToSrgbCore(ReadOnlySpan<float> input, PdfRenderingIntent intent);
