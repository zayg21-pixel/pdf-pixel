using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color.Clut
{
    internal delegate SKColor DeviceToSrgbCore(ReadOnlySpan<float> input, PdfRenderingIntent intent);
}
