using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Text;
using System;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Records text highlights in unscaled page space.
/// </summary>
internal static class PdfPanelHighlightBuilder
{
    /// <summary>
    /// Records the characters of <paramref name="range"/> as one strip per line, merging characters whose top differs
    /// by less than <paramref name="lineMergeThreshold"/> of the strip height.
    /// </summary>
    public static void DrawHighlight(PdfCommandRecorder recorder, PdfWord[] words, in PdfPanelTextRange range, PdfPaint paint, float lineMergeThreshold)
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
                DrawStrip(recorder, currentStrip.Value, paint);
                currentStrip = box;
            }
        }

        if (currentStrip != null)
        {
            DrawStrip(recorder, currentStrip.Value, paint);
        }
    }

    private static void DrawStrip(PdfCommandRecorder recorder, in PdfRectangle strip, PdfPaint paint)
    {
        PdfPathBuilder builder = new();
        builder.AddRect(strip);

        recorder.Process(new DrawPathCommand(builder.ToPath(), paint));
    }
}
