using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws an image into a destination rectangle with sampling options.
/// Takes ownership of both the image and the paint.
/// </summary>
public sealed class DrawImageCommand : PdfCommand
{
    private readonly SKImage _image;
    private readonly SKRect _destRect;
    private readonly SKSamplingOptions _sampling;
    private readonly SKPaint _basePaint;

    public DrawImageCommand(SKImage image, SKRect destRect, SKSamplingOptions sampling, SKPaint basePaint)
    {
        _image = image;
        _destRect = destRect;
        _sampling = sampling;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        using var paint = _basePaint.Clone();
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }
        canvas.DrawImage(_image, _destRect, _sampling, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _image.Dispose();
        _basePaint.Dispose();
    }
}
