using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Flattens a <see cref="PdfTextBlock"/> tree into a linear sequence of characters in reading order,
/// excluding artifacts and substituting /ActualText replacements.
/// </summary>
public static class PdfTextBlockFlattener
{
    /// <summary>
    /// Flattens the given text block tree into a list of characters.
    /// </summary>
    public static List<PdfCharacter> Flatten(PdfTextBlock rootTextBlock)
    {
        if (rootTextBlock == null)
        {
            return new List<PdfCharacter>();
        }

        List<PdfCharacter> result = [];
        FlattenRecursive(rootTextBlock, result);
        return result;
    }

    private static void FlattenRecursive(PdfTextBlock block, List<PdfCharacter> result)
    {
        if (block.Markup?.IsArtifact == true)
        {
            return;
        }

        if (block.Markup?.ActualText != null)
        {
            PdfRectangle? unionBounds = null;
            CollectBounds(block, ref unionBounds);
            if (unionBounds != null)
            {
                result.Add(new PdfCharacter(block.Markup.ActualText.Value.ToString(), unionBounds.Value));
            }

            return;
        }

        foreach (PdfCharacter character in block.Characters)
        {
            result.Add(character);
        }

        foreach (PdfTextBlock child in block.Children)
        {
            FlattenRecursive(child, result);
        }
    }

    private static void CollectBounds(PdfTextBlock block, ref PdfRectangle? bounds)
    {
        foreach (PdfCharacter character in block.Characters)
        {
            bounds = (bounds == null)
                ? character.BoundingBox
                : PdfRectangle.Union(bounds.Value, character.BoundingBox);
        }

        foreach (PdfTextBlock child in block.Children)
        {
            CollectBounds(child, ref bounds);
        }
    }
}
