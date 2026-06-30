using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Resources;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Unified entry point for annotation fallback graphics rendering.
/// </summary>
internal static class PdfAnnotationGraphics
{
    private const string BubbleIconName = "Bubble";

    /// <summary>
    /// Default size of the annotation bubble in user units.
    /// </summary>
    internal const float DefaultBubbleSize = 16f;

    /// <summary>
    /// Default border color used when rendering a bubble with no annotation color defined.
    /// </summary>
    internal static readonly SKColor DefaultBubbleBorderColor = new(180, 140, 60);

    /// <summary>
    /// Default background color used when rendering a bubble with no annotation interior color defined.
    /// </summary>
    internal static readonly SKColor DefaultBubbleBackgroundColor = new(255, 255, 235);

    private static readonly Dictionary<(string Name, PdfAnnotationVisualStateKind State), PdfAnnotationIconDefinition> Icons = [];

    static PdfAnnotationGraphics()
    {
        byte[] resourceBytes = PdfResourceLoader.GetResource("AnnotationIcons.xml");
        string xmlContent = Encoding.UTF8.GetString(resourceBytes);
        var document = XDocument.Parse(xmlContent);
        XElement? rootElement = document.Root;

        if (rootElement != null)
        {
            foreach (XElement iconElement in rootElement.Elements("Icon"))
            {
                PdfAnnotationIconDefinition definition = PdfAnnotationIconParser.Parse(iconElement);
                Icons[(definition.Name, definition.VisualState)] = definition;
            }
        }
    }

    /// <summary>
    /// Returns the icon definition for the given name and visual state, falling back to
    /// <see cref="PdfAnnotationVisualStateKind.Rollover"/> then
    /// <see cref="PdfAnnotationVisualStateKind.Normal"/> if the requested state is not defined.
    /// </summary>
    public static PdfAnnotationIconDefinition? GetAnnotationIcon(string name, PdfAnnotationVisualStateKind state)
    {
        if (Icons.TryGetValue((name, state), out PdfAnnotationIconDefinition? definition))
        {
            return definition;
        }

        if (state == PdfAnnotationVisualStateKind.Down
            && Icons.TryGetValue((name, PdfAnnotationVisualStateKind.Rollover), out definition))
        {
            return definition;
        }

        Icons.TryGetValue((name, PdfAnnotationVisualStateKind.Normal), out definition);
        return definition;
    }

    /// <summary>
    /// Returns the annotation speech bubble icon for the given visual state.
    /// </summary>
    public static PdfAnnotationIconDefinition? GetAnnotationBubbleIcon(PdfAnnotationVisualStateKind state)
        => GetAnnotationIcon(BubbleIconName, state);

    /// <summary>
    /// Renders an icon definition scaled to fit <paramref name="rect"/>.
    /// </summary>
    /// <param name="processor">
    /// The command processor to emit commands to.
    /// </param>
    /// <param name="icon">
    /// The icon definition to render.
    /// </param>
    /// <param name="rect">
    /// The target rectangle in current coordinate space.
    /// </param>
    /// <param name="color">
    /// The exterior (border/stroke) color, used for
    /// <see cref="PdfAnnotationIconColorType.Exterior"/> paths.
    /// </param>
    /// <param name="interiorColor">
    /// The interior (fill) color, used for
    /// <see cref="PdfAnnotationIconColorType.Interior"/> paths.
    /// Falls back to <paramref name="color"/> when null.
    /// </param>
    public static void RenderIcon(
        IPdfCommandProcessor processor,
        PdfAnnotationIconDefinition icon,
        SKRect rect,
        in SKColor color,
        SKColor? interiorColor)
    {
        float scale = Math.Min(rect.Width / icon.Width, rect.Height / icon.Height);
        float offsetX = rect.Left + ((rect.Width - (icon.Width * scale)) / 2f);
        float offsetY = rect.Top + ((rect.Height - (icon.Height * scale)) / 2f);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(
            SKMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY)));
        processor.Process(new ConcatMatrixCommand(icon.ViewportMatrix));

        foreach (PdfAnnotationIconPath iconPath in icon.Paths)
        {
            RenderIconPath(processor, iconPath, color, interiorColor);
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Draws a line ending at <paramref name="point"/>, oriented away from <paramref name="adjacent"/>.
    /// </summary>
    /// <param name="processor">
    /// The command processor to emit commands to.
    /// </param>
    /// <param name="point">
    /// The endpoint at which to draw the line ending.
    /// </param>
    /// <param name="adjacent">
    /// The neighbouring vertex used to determine orientation.
    /// </param>
    /// <param name="style">
    /// The line ending style.
    /// </param>
    /// <param name="lineWidth">
    /// The line width.
    /// </param>
    /// <param name="lineColor">
    /// The stroke color.
    /// </param>
    /// <param name="interiorColor">
    /// The fill color for closed shapes.
    /// </param>
    public static void DrawLineEnding(
        IPdfCommandProcessor processor,
        SKPoint point,
        SKPoint adjacent,
        PdfLineEndingStyle style,
        float lineWidth,
        in SKColor lineColor,
        SKColor? interiorColor)
    {
        PdfAnnotationLineEndingRenderer.DrawLineEnding(
            processor,
            point.X,
            point.Y,
            adjacent.X,
            adjacent.Y,
            style,
            lineWidth,
            lineColor,
            interiorColor);
    }

    private static void RenderIconPath(
        IPdfCommandProcessor processor,
        PdfAnnotationIconPath iconPath,
        in SKColor color,
        SKColor? interiorColor)
    {
        if (iconPath.FillColorType != PdfAnnotationIconColorType.None)
        {
            SKColor? fillColor = iconPath.FillColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorColor,
                PdfAnnotationIconColorType.Override => iconPath.FillColor,
                _ => color
            };

            if (fillColor != null)
            {
                SKPaint fillPaint = new()
                {
                    Style = SKPaintStyle.Fill,
                    Color = ApplyOpacity(fillColor.Value, iconPath.FillOpacity)
                };

                processor.Process(new DrawPathCommand(new SKPath(iconPath.Path), fillPaint));
            }
        }

        if (iconPath.StrokeColorType != PdfAnnotationIconColorType.None)
        {
            SKColor? strokeColor = iconPath.StrokeColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorColor,
                PdfAnnotationIconColorType.Override => iconPath.StrokeColor,
                _ => color
            };

            if (strokeColor != null)
            {
                SKPaint strokePaint = new()
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = iconPath.StrokeWidth,
                    StrokeCap = iconPath.StrokeCap,
                    StrokeJoin = iconPath.StrokeJoin,
                    Color = ApplyOpacity(strokeColor.Value, iconPath.StrokeOpacity)
                };

                processor.Process(new DrawPathCommand(new SKPath(iconPath.Path), strokePaint));
            }
        }
    }

    private static SKColor ApplyOpacity(in SKColor color, float opacity)
        => (opacity >= 1f) ? color : color.WithAlpha((byte)(color.Alpha * opacity));
}
