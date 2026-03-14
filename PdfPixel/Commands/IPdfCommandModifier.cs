using SkiaSharp;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Allows modification of paint properties before a command draws with it.
/// </summary>
public interface IPdfCommandModifier : IDisposable
{
    /// <summary>
    /// Modifies the specified paint in place before it is used for drawing.
    /// </summary>
    /// <param name="paint">The paint to modify.</param>
    void ModifyPaint(SKPaint paint);
}
