using PdfPixel.Models;
using System;
using System.IO;
using System.Text;
using System.Xml;

namespace PdfPixel.Text;

/// <summary>
/// Extracts plain text from the XHTML-subset markup used by an annotation's rich text
/// contents (the RC entry), as defined in ISO 32000-1 Annex E.
/// </summary>
internal static class PdfRichTextContentParser
{
    /// <summary>
    /// Converts rich text markup into plain text by discarding all tags and attributes.
    /// </summary>
    /// <param name="richText">The rich text markup to convert.</param>
    /// <returns>The plain text content, or <c>null</c> when <paramref name="richText"/> is absent or is not valid markup.</returns>
    public static PdfString? ExtractPlainText(PdfString? richText)
    {
        if (richText == null)
        {
            return null;
        }

        XmlReaderSettings settings = new()
        {
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Parse,
            MaxCharactersFromEntities = 10_000_000
        };

        try
        {
            using StringReader textReader = new(richText.Value.ToString());
            using XmlReader reader = XmlReader.Create(textReader, settings);
            StringBuilder builder = new();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.SignificantWhitespace:
                    {
                        builder.Append(reader.Value);
                        break;
                    }
                    case XmlNodeType.Element when reader.IsEmptyElement:
                    case XmlNodeType.EndElement:
                    {
                        if (reader.Depth > 0 && reader.LocalName is "br" or "p" or "div")
                        {
                            builder.Append('\n');
                        }

                        break;
                    }
                }
            }

            return (PdfString)builder.ToString().Trim();
        }
        catch (Exception exception) when (exception is XmlException or ArgumentException)
        {
            return null;
        }
    }
}
