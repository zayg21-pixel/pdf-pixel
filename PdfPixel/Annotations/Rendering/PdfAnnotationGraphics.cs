using PdfPixel.Annotations;
using PdfPixel.Annotations.Icons;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.EmbeddedData;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

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
    internal static readonly float[] DefaultBubbleBorderColor = [180f / 255f, 140f / 255f, 60f / 255f];

    /// <summary>
    /// Default background color used when rendering a bubble with no annotation interior color defined.
    /// </summary>
    internal static readonly float[] DefaultBubbleBackgroundColor = [255f / 255f, 255f / 255f, 235f / 255f];

    /// <summary>
    /// Color the exterior paths of annotation icons are drawn in.
    /// </summary>
    internal static readonly float[] IconExteriorColor = [0f];

    private static readonly Dictionary<(string Name, PdfAnnotationVisualStateKind State), PdfAnnotationIconDefinition> Icons = [];

    static PdfAnnotationGraphics()
    {
        byte[] resourceBytes = PdfResourceLoader.GetResource("AnnotationIcons.xml");
        using MemoryStream stream = new(resourceBytes);
        using XmlReader reader = XmlReader.Create(stream);

        while (reader.ReadToFollowing("Icon"))
        {
            PdfAnnotationIconDefinition definition = PdfAnnotationIconParser.Parse(reader);
            Icons[(definition.Name, definition.VisualState)] = definition;
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
    /// <param name="page">
    /// The PDF page the color overrides of the icon are resolved for.
    /// </param>
    /// <param name="icon">
    /// The icon definition to render.
    /// </param>
    /// <param name="rect">
    /// The target rectangle in current coordinate space.
    /// </param>
    /// <param name="paint">
    /// The exterior (border/stroke) paint, used for
    /// <see cref="PdfAnnotationIconColorType.Exterior"/> paths, or null to leave them out.
    /// </param>
    /// <param name="interiorPaint">
    /// The interior (fill) paint, used for
    /// <see cref="PdfAnnotationIconColorType.Interior"/> paths, or null to leave them out.
    /// </param>
    public static void RenderIcon(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationIconDefinition icon,
        in PdfRectangle rect,
        PdfPaint? paint,
        PdfPaint? interiorPaint)
    {
        float scale = Math.Min(rect.Width / icon.Width, rect.Height / icon.Height);
        float offsetX = rect.Left + ((rect.Width - (icon.Width * scale)) / 2f);
        float offsetY = rect.Top + ((rect.Height - (icon.Height * scale)) / 2f);

        processor.Process(SaveStateCommand.Instance);

        bool useOpacityLayer = icon.Opacity < 1f;
        if (useOpacityLayer)
        {
            processor.Process(new SaveLayerCommand(rect, PdfAnnotationPaintFactory.CreateOpacityLayerPaint(icon.Opacity)));
        }

        processor.Process(new ConcatMatrixCommand(
            PdfMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY)));
        processor.Process(new ConcatMatrixCommand(icon.ViewportMatrix));

        foreach (PdfAnnotationIconPath iconPath in icon.Paths)
        {
            RenderIconPath(processor, page, iconPath, paint, interiorPaint);
        }

        if (useOpacityLayer)
        {
            processor.Process(RestoreLayerCommand.Instance);
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
    /// <param name="linePaint">
    /// The paint whose color the ending is stroked in, or null when the line has none.
    /// </param>
    /// <param name="interiorPaint">
    /// The paint closed shapes are filled with, or null when they have no interior.
    /// </param>
    public static void DrawLineEnding(
        IPdfCommandProcessor processor,
        in PdfPoint point,
        in PdfPoint adjacent,
        PdfLineEndingStyle style,
        float lineWidth,
        PdfPaint? linePaint,
        PdfPaint? interiorPaint)
    {
        PdfAnnotationLineEndingRenderer.DrawLineEnding(
            processor,
            point.X,
            point.Y,
            adjacent.X,
            adjacent.Y,
            style,
            lineWidth,
            linePaint,
            interiorPaint);
    }

    private static void RenderIconPath(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationIconPath iconPath,
        PdfPaint? paint,
        PdfPaint? interiorPaint)
    {
        if (iconPath.FillColorType != PdfAnnotationIconColorType.None)
        {
            PdfPaint? fillPaint = iconPath.FillColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorPaint,
                PdfAnnotationIconColorType.Override => CreateOverridePaint(page, iconPath.FillColor),
                _ => paint
            };

            if (fillPaint != null)
            {
                processor.Process(new DrawPathCommand(iconPath.Path, ApplyOpacity(fillPaint, iconPath.FillOpacity)));
            }
        }

        if (iconPath.StrokeColorType != PdfAnnotationIconColorType.None)
        {
            PdfPaint? strokeColorPaint = iconPath.StrokeColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorPaint,
                PdfAnnotationIconColorType.Override => CreateOverridePaint(page, iconPath.StrokeColor),
                _ => paint
            };

            if (strokeColorPaint != null)
            {
                PdfStrokeStyle strokeStyle = new(
                    lineWidth: iconPath.StrokeWidth,
                    lineCap: iconPath.StrokeCap,
                    lineJoin: iconPath.StrokeJoin);

                PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(ApplyOpacity(strokeColorPaint, iconPath.StrokeOpacity), strokeStyle);
                processor.Process(new DrawPathCommand(iconPath.Path, strokePaint));
            }
        }
    }

    private static PdfPaint? CreateOverridePaint(IPdfPageInternal page, PdfRgbaColor? color)
    {
        if (color == null)
        {
            return null;
        }

        PdfRgbaColor overrideColor = color.Value;

        return PdfAnnotationPaintFactory.CreatePaint([overrideColor.Red, overrideColor.Green, overrideColor.Blue], page).WithAlpha(overrideColor.Alpha);
    }

    private static PdfPaint ApplyOpacity(PdfPaint paint, float opacity)
        => (opacity >= 1f) ? paint : paint.WithAlpha(paint.Alpha * opacity);
}
