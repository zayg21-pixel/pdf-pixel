using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Shading.Model;
using PdfPixel.Transparency.Model;
using PdfPixel.Skia.Converters;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia;

/// <summary>
/// Single entry point for converting <c>Pdf*</c> model types to their SkiaSharp equivalents.
/// Trivial field-mapping conversions are implemented directly here; conversions with real logic
/// delegate to a dedicated class in <see cref="Converters"/>.
/// </summary>
public static class PdfToSkiaExtensions
{
    /// <summary>
    /// Converts a <see cref="PdfMatrix"/> to an <see cref="SKMatrix"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix ToSkMatrix(this in PdfMatrix matrix)
        => new(matrix.ScaleX, matrix.SkewX, matrix.TransX, matrix.SkewY, matrix.ScaleY, matrix.TransY, 0, 0, 1);

    /// <summary>
    /// Converts a <see cref="PdfPoint"/> to an <see cref="SKPoint"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPoint ToSkPoint(this in PdfPoint point) => new(point.X, point.Y);

    /// <summary>
    /// Converts a <see cref="PdfRectangle"/> to an <see cref="SKRect"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRect ToSkRect(this in PdfRectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    /// <summary>
    /// Converts a <see cref="PdfIntegerRectangle"/> to an <see cref="SKRectI"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRectI ToSkRectI(this in PdfIntegerRectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    /// <summary>
    /// Converts a <see cref="PdfSize"/> to an <see cref="SKSize"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKSize ToSkSize(this in PdfSize size) => new(size.Width, size.Height);

    /// <summary>
    /// Converts a <see cref="PdfColor"/> to an <see cref="SKColor"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor ToSkiaColor(this in PdfColor color)
        => new(ToByte(color.Red), ToByte(color.Green), ToByte(color.Blue), ToByte(color.Alpha));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToByte(float channel) => (byte)((Math.Max(0f, Math.Min(1f, channel)) * 255f) + 0.5f);

    /// <summary>
    /// Converts a <see cref="PdfClipOperation"/> to the corresponding <see cref="SKClipOperation"/>.
    /// </summary>
    public static SKClipOperation ToSkClipOperation(this PdfClipOperation operation)
    {
        return operation switch
        {
            PdfClipOperation.Intersect => SKClipOperation.Intersect,
            PdfClipOperation.Difference => SKClipOperation.Difference,
            _ => SKClipOperation.Intersect
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfBlendMode"/> to the corresponding <see cref="SKBlendMode"/>.
    /// Note: Some PDF blend modes may not have exact SkiaSharp equivalents.
    /// </summary>
    public static SKBlendMode ToSkiaBlendMode(this PdfBlendMode pdfBlendMode)
    {
        return pdfBlendMode switch
        {
            PdfBlendMode.Normal => SKBlendMode.SrcOver,
            PdfBlendMode.Multiply => SKBlendMode.Multiply,
            PdfBlendMode.Screen => SKBlendMode.Screen,
            PdfBlendMode.Overlay => SKBlendMode.Overlay,
            PdfBlendMode.SoftLight => SKBlendMode.SoftLight,
            PdfBlendMode.HardLight => SKBlendMode.HardLight,
            PdfBlendMode.ColorDodge => SKBlendMode.ColorDodge,
            PdfBlendMode.ColorBurn => SKBlendMode.ColorBurn,
            PdfBlendMode.Darken => SKBlendMode.Darken,
            PdfBlendMode.Lighten => SKBlendMode.Lighten,
            PdfBlendMode.Difference => SKBlendMode.Difference,
            PdfBlendMode.Exclusion => SKBlendMode.Exclusion,
            PdfBlendMode.Hue => SKBlendMode.Hue,
            PdfBlendMode.Saturation => SKBlendMode.Saturation,
            PdfBlendMode.Color => SKBlendMode.Color,
            PdfBlendMode.Luminosity => SKBlendMode.Luminosity,
            PdfBlendMode.Compatible => SKBlendMode.SrcOver, // Default to normal
            _ => SKBlendMode.SrcOver // Default fallback
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfStrokeCap"/> to the corresponding <see cref="SKStrokeCap"/>.
    /// </summary>
    public static SKStrokeCap ToSkiaStrokeCap(this PdfStrokeCap strokeCap)
    {
        return strokeCap switch
        {
            PdfStrokeCap.Round => SKStrokeCap.Round,
            PdfStrokeCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfStrokeJoin"/> to the corresponding <see cref="SKStrokeJoin"/>.
    /// </summary>
    public static SKStrokeJoin ToSkiaStrokeJoin(this PdfStrokeJoin strokeJoin)
    {
        return strokeJoin switch
        {
            PdfStrokeJoin.Round => SKStrokeJoin.Round,
            PdfStrokeJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfPaint"/> to an <see cref="SKPaint"/>.
    /// </summary>
    public static SKPaint ToSkiaPaint(this PdfPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        return PdfPaintConverter.ToSkiaPaint(paint);
    }

    /// <summary>
    /// Converts a <see cref="PdfPaint"/> to an <see cref="SKPaint"/> that fills the outline a stroke has
    /// already been widened, dashed, capped, and joined into.
    /// </summary>
    public static SKPaint ToSkiaOutlineFillPaint(this PdfPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        return PdfPaintConverter.ToSkiaOutlineFillPaint(paint);
    }

    /// <summary>
    /// Converts a <see cref="PdfPath"/> to an <see cref="SKPath"/>.
    /// </summary>
    public static SKPath ToSkPath(this PdfPath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return PdfPathConverter.ToSkPath(path);
    }

    /// <summary>
    /// Converts a <see cref="PdfImageColorFormat"/> to the matching <see cref="SKColorType"/>.
    /// </summary>
    public static SKColorType ToSkColorType(this PdfImageColorFormat colorFormat)
    {
        return colorFormat switch
        {
            PdfImageColorFormat.Gray => SKColorType.Gray8,
            _ => SKColorType.Rgba8888
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfImageAlphaType"/> to the matching <see cref="SKAlphaType"/>.
    /// </summary>
    public static SKAlphaType ToSkAlphaType(this PdfImageAlphaType alphaType)
    {
        return alphaType switch
        {
            PdfImageAlphaType.Premultiplied => SKAlphaType.Premul,
            PdfImageAlphaType.Unpremultiplied => SKAlphaType.Unpremul,
            _ => SKAlphaType.Opaque
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfDecodedImage"/> to an <see cref="SKImage"/>.
    /// </summary>
    public static SKImage ToSkImage(this PdfDecodedImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        return PdfImageConverter.ToSkImage(image);
    }

    /// <summary>
    /// Converts <see cref="PdfVertices"/> to <see cref="SKVertices"/>.
    /// </summary>
    public static SKVertices ToSkVertices(this PdfVertices vertices)
    {
        if (vertices == null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        return PdfShadingConverter.ToSkVertices(vertices);
    }

    /// <summary>
    /// Builds the shader for an axial (Type 2) gradient.
    /// </summary>
    public static SKShader ToSkiaShader(this PdfLinearGradient gradient)
    {
        if (gradient == null)
        {
            throw new ArgumentNullException(nameof(gradient));
        }

        return PdfShadingConverter.ToSkiaShader(gradient);
    }

    /// <summary>
    /// Builds the outer-cone shader for a radial (Type 3) gradient.
    /// </summary>
    public static SKShader ToSkiaOuterShader(this PdfRadialGradient gradient)
    {
        if (gradient == null)
        {
            throw new ArgumentNullException(nameof(gradient));
        }

        return PdfShadingConverter.ToSkiaOuterShader(gradient);
    }

    /// <summary>
    /// Builds the inner-cone shader for a radial (Type 3) gradient.
    /// </summary>
    public static SKShader ToSkiaInnerShader(this PdfRadialGradient gradient)
    {
        if (gradient == null)
        {
            throw new ArgumentNullException(nameof(gradient));
        }

        return PdfShadingConverter.ToSkiaInnerShader(gradient);
    }
}
