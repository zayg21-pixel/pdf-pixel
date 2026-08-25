using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

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
    /// <param name="iconElement">The <c>&lt;Icon&gt;</c> element to parse.</param>
    public static PdfAnnotationIconDefinition Parse(XElement iconElement)
    {
        string name = RequireAttribute(iconElement, "name");
        float width = ParseFloat(iconElement, "width");
        float height = ParseFloat(iconElement, "height");
        string stateValue = iconElement.Attribute("state")?.Value ?? "Normal";
        PdfAnnotationVisualStateKind visualState = ParseVisualState(stateValue);

        PdfMatrix viewportMatrix;
        XAttribute? viewBoxAttribute = iconElement.Attribute("viewBox");

        viewportMatrix = (viewBoxAttribute != null)
            ? ParseViewBox(viewBoxAttribute.Value, width, height)
            : PdfMatrix.CreateScaleTranslation(1, -1, 0, height);

        List<PdfAnnotationIconPath> paths = [];

        foreach (XElement pathElement in iconElement.Elements("path"))
        {
            paths.Add(ParsePath(pathElement));
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

    private static PdfAnnotationIconPath ParsePath(XElement pathElement)
    {
        string pathData = RequireAttribute(pathElement, "d");
        PdfPathFillType fillType = ParseFillType(pathElement.Attribute("fill-rule")?.Value ?? "nonzero");
        PdfPath path = PdfAnnotationSvgPathParser.Parse(pathData, fillType);

        (PdfAnnotationIconColorType fillColorType, PdfColor fillColor) = ParseColorValue(pathElement.Attribute("fill")?.Value);
        (PdfAnnotationIconColorType strokeColorType, PdfColor strokeColor) = ParseColorValue(pathElement.Attribute("stroke")?.Value);

        float strokeWidth = ParseFloatValue(pathElement.Attribute("stroke-width")?.Value ?? "1");
        PdfStrokeCap strokeCap = ParseStrokeCap(pathElement.Attribute("stroke-linecap")?.Value ?? "butt");
        PdfStrokeJoin strokeJoin = ParseStrokeJoin(pathElement.Attribute("stroke-linejoin")?.Value ?? "miter");
        float fillOpacity = ParseFloatValue(pathElement.Attribute("fill-opacity")?.Value ?? "1");
        float strokeOpacity = ParseFloatValue(pathElement.Attribute("stroke-opacity")?.Value ?? "1");

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

    private static float ParseFloat(XElement element, string attributeName)
        => ParseFloatValue(RequireAttribute(element, attributeName));

    private static float ParseFloatValue(string value)
        => float.Parse(value, CultureInfo.InvariantCulture);

    private static string RequireAttribute(XElement element, string attributeName)
    {
        XAttribute? attribute = element.Attribute(attributeName);

        if (attribute == null)
        {
            throw new InvalidOperationException($"Missing required attribute '{attributeName}' on <{element.Name}>.");
        }

        return attribute.Value;
    }
}
