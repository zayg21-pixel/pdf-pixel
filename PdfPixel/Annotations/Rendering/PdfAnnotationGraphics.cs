using PdfPixel.Annotations;
using PdfPixel.Annotations.Icons;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.EmbeddedData;
using PdfPixel.Geometry;
using PdfPixel.Transparency;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Unified entry point for annotation fallback graphics rendering.
/// </summary>
public static class PdfAnnotationGraphics
{
    private const string BubbleIconName = "Bubble";

    /// <summary>
    /// Paint the exterior paths of annotation icons are drawn with.
    /// </summary>
    internal static readonly PdfPaint IconExteriorPaint = PdfAnnotationPaintFactory.CreatePaint([0f]);

    private static readonly Dictionary<(string Name, PdfAnnotationVisualStateKind State), PdfAnnotationIconDefinition> Icons = [];

    private static readonly PdfTransparencyGroupContext OpacityGroup = new(null, isolated: true, knockout: false);

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
    internal static PdfAnnotationIconDefinition? GetAnnotationIcon(string name, PdfAnnotationVisualStateKind state)
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
    /// Returns the visual states the icon of the given name has a definition for, or
    /// <see cref="PdfAnnotationVisualStateKind.None"/> when there is no such icon.
    /// </summary>
    internal static PdfAnnotationVisualStateKind GetAnnotationIconStates(string name)
    {
        var states = PdfAnnotationVisualStateKind.None;

        if (Icons.ContainsKey((name, PdfAnnotationVisualStateKind.Normal)))
        {
            states |= PdfAnnotationVisualStateKind.Normal;
        }

        if (Icons.ContainsKey((name, PdfAnnotationVisualStateKind.Rollover)))
        {
            states |= PdfAnnotationVisualStateKind.Rollover;
        }

        if (Icons.ContainsKey((name, PdfAnnotationVisualStateKind.Down)))
        {
            states |= PdfAnnotationVisualStateKind.Down;
        }

        return states;
    }

    /// <summary>
    /// Returns the visual states of the annotation speech bubble icon.
    /// </summary>
    internal static PdfAnnotationVisualStateKind GetAnnotationBubbleIconStates() => GetAnnotationIconStates(BubbleIconName);

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
        PdfAnnotationIconDefinition icon,
        in PdfRectangle rect,
        PdfPaint? paint,
        PdfPaint? interiorPaint)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (icon == null)
        {
            throw new ArgumentNullException(nameof(icon));
        }

        float scale = Math.Min(rect.Width / icon.Width, rect.Height / icon.Height);
        float offsetX = rect.Left + ((rect.Width - (icon.Width * scale)) / 2f);
        float offsetY = rect.Top + ((rect.Height - (icon.Height * scale)) / 2f);

        PdfMatrix iconMatrix = PdfMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY);

        processor.Process(SaveStateCommand.Instance);

        if (icon.Opacity < 1f)
        {
            PdfCommandRecorder recorder = new();
            RenderIconPaths(recorder, icon, iconMatrix, paint, interiorPaint);
            DrawOpacityGroup(processor, rect, recorder, icon.Opacity);
        }
        else
        {
            RenderIconPaths(processor, icon, iconMatrix, paint, interiorPaint);
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Paints <paramref name="recorder"/> as an isolated group composited with <paramref name="opacity"/>.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="bounds">The area the recorded content can reach, in current coordinate space.</param>
    /// <param name="recorder">The recorded content.</param>
    /// <param name="opacity">The opacity the group is composited with.</param>
    internal static void DrawOpacityGroup(IPdfCommandProcessor processor, in PdfRectangle bounds, PdfCommandRecorder recorder, float opacity)
    {
        PdfGroupSource source = new(new DrawRecordingCommand(recorder), OpacityGroup, bounds, null);
        processor.Process(new DrawGroupCommand(source, PdfAnnotationPaintFactory.CreateOpacityLayerPaint(opacity)));
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
    internal static void DrawLineEnding(
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

    private static void RenderIconPaths(
        IPdfCommandProcessor processor,
        PdfAnnotationIconDefinition icon,
        in PdfMatrix iconMatrix,
        PdfPaint? paint,
        PdfPaint? interiorPaint)
    {
        processor.Process(new ConcatMatrixCommand(iconMatrix));
        processor.Process(new ConcatMatrixCommand(icon.ViewportMatrix));

        foreach (PdfAnnotationIconPath iconPath in icon.Paths)
        {
            RenderIconPath(processor, iconPath, paint, interiorPaint);
        }
    }

    private static void RenderIconPath(
        IPdfCommandProcessor processor,
        PdfAnnotationIconPath iconPath,
        PdfPaint? paint,
        PdfPaint? interiorPaint)
    {
        if (iconPath.FillColorType != PdfAnnotationIconColorType.None)
        {
            PdfPaint? fillPaint = iconPath.FillColorType switch
            {
                PdfAnnotationIconColorType.Interior => interiorPaint,
                PdfAnnotationIconColorType.Override => iconPath.FillPaint,
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
                PdfAnnotationIconColorType.Override => iconPath.StrokePaint,
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

    private static PdfPaint ApplyOpacity(PdfPaint paint, float opacity)
        => (opacity >= 1f) ? paint : paint.WithAlpha(paint.Alpha * opacity);
}
