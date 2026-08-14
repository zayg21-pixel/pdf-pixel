using PdfPixel.Models;
using System;
using System.Text;
using System.Xml.Linq;

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

        try
        {
            var document = XDocument.Parse(richText.Value.ToString(), LoadOptions.None);
            StringBuilder builder = new();
            AppendText(document.Root, builder);
            return (PdfString)builder.ToString().Trim();
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            return null;
        }
    }

    private static void AppendText(XElement? element, StringBuilder builder)
    {
        if (element == null)
        {
            return;
        }

        foreach (XNode node in element.Nodes())
        {
            if (node is XText textNode)
            {
                builder.Append(textNode.Value);
            }
            else if (node is XElement childElement)
            {
                AppendText(childElement, builder);

                if (childElement.Name.LocalName is "br" or "p" or "div")
                {
                    builder.Append('\n');
                }
            }
        }
    }
}
