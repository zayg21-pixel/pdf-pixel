using PdfPixel.Geometry;
using PdfPixel.Skia;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Draws text highlights in unscaled page space.
/// </summary>
internal static class PdfPanelHighlightBuilder
{
    /// <summary>
    /// Draws the characters of <paramref name="range"/> as one strip per line, merging characters whose top differs
    /// by less than <paramref name="lineMergeThreshold"/> of the strip height.
    /// </summary>
    public static void DrawHighlight(SKCanvas canvas, PdfWord[] words, in PdfPanelTextRange range, SKPaint paint, float lineMergeThreshold)
    {
        PdfRectangle? currentStrip = null;

        foreach (PdfCharacter character in PdfPanelText.EnumerateCharacters(words, range))
        {
            PdfRectangle box = character.BoundingBox;

            if (currentStrip == null)
            {
                currentStrip = box;
            }
            else if (Math.Abs(box.Top - currentStrip.Value.Top) < currentStrip.Value.Height * lineMergeThreshold)
            {
                currentStrip = PdfRectangle.Union(currentStrip.Value, box);
            }
            else
            {
                canvas.DrawRect(currentStrip.Value.ToSkRect(), paint);
                currentStrip = box;
            }
        }

        if (currentStrip != null)
        {
            canvas.DrawRect(currentStrip.Value.ToSkRect(), paint);
        }
    }
}
