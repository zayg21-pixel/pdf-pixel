using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Resources;
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
    internal static readonly PdfColor DefaultBubbleBorderColor = new(180f / 255f, 140f / 255f, 60f / 255f);

    /// <summary>
    /// Default background color used when rendering a bubble with no annotation interior color defined.
    /// </summary>
    internal static readonly PdfColor DefaultBubbleBackgroundColor = new(255f / 255f, 255f / 255f, 235f / 255f);

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
        in PdfRectangle rect,
        in PdfColor color,
        PdfColor? interiorColor)
    {
        float scale = Math.Min(rect.Width / icon.Width, rect.Height / icon.Height);
        float offsetX = rect.Left + ((rect.Width - (icon.Width * scale)) / 2f);
        float offsetY = rect.Top + ((rect.Height - (icon.Height * scale)) / 2f);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(
            PdfMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY)));
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
        in PdfPoint point,
        in PdfPoint adjacent,
        PdfLineEndingStyle style,
        float lineWidth,
        in PdfColor lineColor,
        PdfColor? interiorColor)
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
        in PdfColor color,
        PdfColor? interiorColor)
    {
        if (iconPath.FillColorType != PdfAnnotationIconColorType.None)
        {
            PdfColor? fillColor = iconPath.FillColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorColor,
                PdfAnnotationIconColorType.Override => iconPath.FillColor,
                _ => color
            };

            if (fillColor != null)
            {
                PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(ApplyOpacity(fillColor.Value, iconPath.FillOpacity));
                processor.Process(new DrawPathCommand(iconPath.Path, fillPaint));
            }
        }

        if (iconPath.StrokeColorType != PdfAnnotationIconColorType.None)
        {
            PdfColor? strokeColor = iconPath.StrokeColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorColor,
                PdfAnnotationIconColorType.Override => iconPath.StrokeColor,
                _ => color
            };

            if (strokeColor != null)
            {
                PdfStrokeStyle strokeStyle = new(
                    lineWidth: iconPath.StrokeWidth,
                    lineCap: iconPath.StrokeCap,
                    lineJoin: iconPath.StrokeJoin);

                PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(ApplyOpacity(strokeColor.Value, iconPath.StrokeOpacity), strokeStyle);
                processor.Process(new DrawPathCommand(iconPath.Path, strokePaint));
            }
        }
    }

    private static PdfColor ApplyOpacity(in PdfColor color, float opacity)
        => (opacity >= 1f) ? color : color.WithAlpha(color.Alpha * opacity);
}
