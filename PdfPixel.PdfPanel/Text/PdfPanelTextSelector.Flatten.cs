using PdfPixel.TextExtraction;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Text;

public sealed partial class PdfPanelTextSelector
{
    private List<PdfCharacter> GetFlattenedCharacters(int pageNumber, PdfTextBlock rootTextBlock)
    {
        if (_flattenedPages.TryGetValue(pageNumber, out List<PdfCharacter>? cached))
        {
            return cached;
        }

        List<PdfCharacter> result = [];
        FlattenTextBlockRecursive(rootTextBlock, result);
        _flattenedPages[pageNumber] = result;
        return result;
    }

    private static void FlattenTextBlockRecursive(PdfTextBlock block, List<PdfCharacter> result)
    {
        if (block.Markup?.IsArtifact == true)
        {
            return;
        }

        if (block.Markup?.ActualText.IsEmpty == false)
        {
            SKRect? unionBounds = null;
            CollectBounds(block, ref unionBounds);
            if (unionBounds != null)
            {
                result.Add(new PdfCharacter(block.Markup.ActualText.ToString(), unionBounds.Value));
            }

            return;
        }

        foreach (PdfCharacter character in block.Characters)
        {
            result.Add(character);
        }

        foreach (PdfTextBlock child in block.Children)
        {
            FlattenTextBlockRecursive(child, result);
        }
    }

    private static void CollectBounds(PdfTextBlock block, ref SKRect? bounds)
    {
        foreach (PdfCharacter character in block.Characters)
        {
            bounds = (bounds == null)
                ? character.BoundingBox
                : SKRect.Union(bounds.Value, character.BoundingBox);
        }

        foreach (PdfTextBlock child in block.Children)
        {
            CollectBounds(child, ref bounds);
        }
    }
}
