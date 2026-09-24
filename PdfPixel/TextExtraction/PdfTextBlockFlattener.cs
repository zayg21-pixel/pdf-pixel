using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Flattens a <see cref="PdfTextBlock"/> tree into a linear sequence of characters in reading order,
/// excluding artifacts and substituting /ActualText replacements.
/// </summary>
public sealed class PdfTextBlockFlattener
{
    private readonly List<PdfCharacter> _buffer = [];

    /// <summary>
    /// Flattens the given text block tree into a list of characters.
    /// </summary>
    /// <param name="rootTextBlock">Root of the text block tree to flatten.</param>
    /// <param name="matrix">Matrix every character bounding box is mapped through.</param>
    public PdfCharacter[] Flatten(PdfTextBlock rootTextBlock, in PdfMatrix matrix)
    {
        if (rootTextBlock == null)
        {
            return Array.Empty<PdfCharacter>();
        }

        _buffer.Clear();
        FlattenRecursive(rootTextBlock, matrix, _buffer);
        return _buffer.ToArray();
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void FlattenRecursive(PdfTextBlock block, in PdfMatrix matrix, List<PdfCharacter> result)
    {
        if (block.Markup?.IsArtifact == true)
        {
            return;
        }

        if (block.Markup?.ActualText != null)
        {
            PdfRectangle? unionBounds = null;
            CollectBounds(block, matrix, ref unionBounds);
            if (unionBounds != null)
            {
                result.Add(new PdfCharacter(block.Markup.ActualText.Value.ToString(), unionBounds.Value));
            }

            return;
        }

        foreach (PdfCharacter character in block.Characters)
        {
            result.Add(new PdfCharacter(character.Text, matrix.MapRect(character.BoundingBox)));
        }

        foreach (PdfTextBlock child in block.Children)
        {
            FlattenRecursive(child, matrix, result);
        }
    }

    private static void CollectBounds(PdfTextBlock block, in PdfMatrix matrix, ref PdfRectangle? bounds)
    {
        foreach (PdfCharacter character in block.Characters)
        {
            PdfRectangle characterBounds = matrix.MapRect(character.BoundingBox);

            bounds = (bounds == null)
                ? characterBounds
                : PdfRectangle.Union(bounds.Value, characterBounds);
        }

        foreach (PdfTextBlock child in block.Children)
        {
            CollectBounds(child, matrix, ref bounds);
        }
    }
}
