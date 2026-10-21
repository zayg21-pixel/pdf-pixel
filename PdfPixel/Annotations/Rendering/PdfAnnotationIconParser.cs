using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Parses icon definitions from the annotation resource XML format.
/// </summary>
/// <remarks>
/// Expected format:
/// <code>
/// &lt;Icon width="24" height="24"&gt;
///   &lt;path d="..." icon-color-type="interior|exterior|override"
///         stroke="#rrggbb|none" fill="#rrggbb|none" stroke-width="1.0" /&gt;
/// &lt;/Icon&gt;
/// </code>
/// </remarks>
internal static class PdfAnnotationIconParser
{
    private const string NoneValue = "none";

    internal static readonly char[] separator = [' ', ',', '\t'];

    /// <summary>
    /// Parses a single <c>&lt;Icon&gt;</c> XML element into a <see cref="PdfAnnotationIconDefinition"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on the <c>&lt;Icon&gt;</c> start element.</param>
    public static PdfAnnotationIconDefinition Parse(XmlReader reader)
    {
        string name = RequireAttribute(reader, "name");
        float width = ParseFloat(reader, "width");
        float height = ParseFloat(reader, "height");
        string stateValue = reader.GetAttribute("state") ?? "Normal";
        PdfAnnotationVisualStateKind visualState = ParseVisualState(stateValue);

        PdfMatrix viewportMatrix;
        string? viewBoxValue = reader.GetAttribute("viewBox");

        viewportMatrix = (viewBoxValue != null)
            ? ParseViewBox(viewBoxValue, width, height)
            : PdfMatrix.CreateScaleTranslation(1, -1, 0, height);

        List<PdfAnnotationIconPath> paths = [];

        if (reader.ReadToDescendant("path"))
        {
            do
            {
                paths.Add(ParsePath(reader));
            }
            while (reader.ReadToNextSibling("path"));
        }

        return new PdfAnnotationIconDefinition(name, width, height, visualState, viewportMatrix, paths.ToArray());
    }

    private static PdfAnnotationVisualStateKind ParseVisualState(string value)
    {
        return value switch
        {
            "Normal" => PdfAnnotationVisualStateKind.Normal,
            "Rollover" => PdfAnnotationVisualStateKind.Rollover,
            "Down" => PdfAnnotationVisualStateKind.Down,
            _ => throw new InvalidOperationException($"Unknown visual state: {value}")
        };
    }

    private static PdfAnnotationIconPath ParsePath(XmlReader reader)
    {
        string pathData = RequireAttribute(reader, "d");
        PdfPathFillType fillType = ParseFillType(reader.GetAttribute("fill-rule") ?? "nonzero");
        PdfPath path = PdfAnnotationSvgPathParser.Parse(pathData, fillType);

        (PdfAnnotationIconColorType fillColorType, PdfColor fillColor) = ParseColorValue(reader.GetAttribute("fill"));
        (PdfAnnotationIconColorType strokeColorType, PdfColor strokeColor) = ParseColorValue(reader.GetAttribute("stroke"));

        float strokeWidth = ParseFloatValue(reader.GetAttribute("stroke-width") ?? "1");
        PdfStrokeCap strokeCap = ParseStrokeCap(reader.GetAttribute("stroke-linecap") ?? "butt");
        PdfStrokeJoin strokeJoin = ParseStrokeJoin(reader.GetAttribute("stroke-linejoin") ?? "miter");
        float fillOpacity = ParseFloatValue(reader.GetAttribute("fill-opacity") ?? "1");
        float strokeOpacity = ParseFloatValue(reader.GetAttribute("stroke-opacity") ?? "1");

        return new PdfAnnotationIconPath(path, fillColorType, fillColor, strokeColorType, strokeColor, strokeWidth, strokeCap, strokeJoin, fillOpacity, strokeOpacity);
    }

    private static (PdfAnnotationIconColorType ColorType, PdfColor Color) ParseColorValue(string? value)
    {
        if (value == null || value.Length == 0 || string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase))
        {
            return (PdfAnnotationIconColorType.None, PdfColors.Transparent);
        }

        if (string.Equals(value, "interior", StringComparison.OrdinalIgnoreCase))
        {
            return (PdfAnnotationIconColorType.Interior, PdfColors.Transparent);
        }

        if (string.Equals(value, "exterior", StringComparison.OrdinalIgnoreCase))
        {
            return (PdfAnnotationIconColorType.Exterior, PdfColors.Transparent);
        }

        return (PdfAnnotationIconColorType.Override, PdfColor.ParseHexColor(value));
    }

    private static PdfMatrix ParseViewBox(string value, float width, float height)
    {
        string[] parts = value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        float minX = ParseFloatValue(parts[0]);
        float minY = ParseFloatValue(parts[1]);
        float vbWidth = ParseFloatValue(parts[2]);
        float vbHeight = ParseFloatValue(parts[3]);

        float scaleX = width / vbWidth;
        float scaleY = height / vbHeight;

        return PdfMatrix.CreateScaleTranslation(scaleX, -scaleY, -minX * scaleX, height + (minY * scaleY));
    }

    private static PdfStrokeCap ParseStrokeCap(string value)
    {
        return value switch
        {
            "round" => PdfStrokeCap.Round,
            "square" => PdfStrokeCap.Square,
            _ => PdfStrokeCap.Butt
        };
    }

    private static PdfStrokeJoin ParseStrokeJoin(string value)
    {
        return value switch
        {
            "round" => PdfStrokeJoin.Round,
            "bevel" => PdfStrokeJoin.Bevel,
            _ => PdfStrokeJoin.Miter
        };
    }

    private static PdfPathFillType ParseFillType(string value)
    {
        return value switch
        {
            "evenodd" => PdfPathFillType.EvenOdd,
            _ => PdfPathFillType.Winding
        };
    }

    private static float ParseFloat(XmlReader reader, string attributeName)
        => ParseFloatValue(RequireAttribute(reader, attributeName));

    private static float ParseFloatValue(string value)
        => float.Parse(value, CultureInfo.InvariantCulture);

    private static string RequireAttribute(XmlReader reader, string attributeName)
    {
        string? value = reader.GetAttribute(attributeName);

        if (value == null)
        {
            throw new InvalidOperationException($"Missing required attribute '{attributeName}' on <{reader.Name}>.");
        }

        return value;
    }
}
