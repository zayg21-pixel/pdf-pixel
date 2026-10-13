using PdfPixel.Commands.Model;
using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// Extracts the words of a content tree in reading order, excluding artifacts and substituting /ActualText replacements.
/// </summary>
public sealed class PdfTextExtractor
{
    private readonly List<PdfCharacter> _characters = [];
    private readonly PdfTextChunker _chunker = new();

    /// <summary>
    /// Extracts the words of the content tree under <paramref name="root"/>, with bounding boxes mapped through <paramref name="matrix"/>.
    /// </summary>
    /// <param name="root">Root of the content tree to extract.</param>
    /// <param name="matrix">Matrix mapping the space of the content tree into page coordinates.</param>
    /// <param name="properties">Properties controlling the extraction.</param>
    public PdfWord[] ExtractWords(PdfContentNode root, in PdfMatrix matrix, PdfTextExtractionProperties properties)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        _characters.Clear();
        CollectCharacters(root, matrix, properties);

        return _chunker.ChunkCharacters(_characters.ToArray(), properties);
    }

    private void CollectCharacters(PdfContentNode node, in PdfMatrix matrix, PdfTextExtractionProperties properties)
    {
        if (IsExcludedContentStream(node.Scope))
        {
            return;
        }

        if (node.Scope is PdfMarkedContentScope markedContent)
        {
            if (markedContent.IsArtifact && !properties.IncludeArtifacts)
            {
                return;
            }

            if (properties.UseActualText)
            {
                PdfString? actualText = markedContent.ActualText ?? markedContent.StructureElement?.ActualText;
                if (actualText != null)
                {
                    AppendActualText(node, matrix, actualText.Value);
                    return;
                }
            }
        }

        foreach (PdfContentElement element in node.Elements)
        {
            switch (element.ElementType)
            {
                case PdfContentElementType.Node:
                {
                    CollectCharacters((PdfContentNode)element, matrix, properties);
                    break;
                }
                case PdfContentElementType.Item:
                {
                    AppendCharacters((PdfContentItem)element, matrix);
                    break;
                }
            }
        }
    }

    private void AppendCharacters(PdfContentItem item, in PdfMatrix matrix)
    {
        if (item.Command.ContentKind != PdfContentKind.Text)
        {
            return;
        }

        var command = (PdfContentCommand<PdfCharacter[]>)item.Command;
        PdfMatrix characterMatrix = matrix.PreConcat(item.Matrix).PreConcat(command.Matrix);

        foreach (PdfCharacter character in command.Content)
        {
            _characters.Add(new PdfCharacter(character.Text, characterMatrix.MapRect(character.BoundingBox)));
        }
    }

    private void AppendActualText(PdfContentNode node, in PdfMatrix matrix, in PdfString actualText)
    {
        PdfRectangle? bounds = null;
        CollectBounds(node, matrix, ref bounds);

        if (bounds != null)
        {
            _characters.Add(new PdfCharacter(actualText.DecodePdfString(), bounds.Value));
        }
    }

    private static void CollectBounds(PdfContentNode node, in PdfMatrix matrix, ref PdfRectangle? bounds)
    {
        if (IsExcludedContentStream(node.Scope))
        {
            return;
        }

        foreach (PdfContentElement element in node.Elements)
        {
            if (element.ElementType == PdfContentElementType.Node)
            {
                CollectBounds((PdfContentNode)element, matrix, ref bounds);
                continue;
            }

            var item = (PdfContentItem)element;
            if (item.Command.ContentKind != PdfContentKind.Text)
            {
                continue;
            }

            var command = (PdfContentCommand<PdfCharacter[]>)item.Command;
            PdfMatrix characterMatrix = matrix.PreConcat(item.Matrix).PreConcat(command.Matrix);

            foreach (PdfCharacter character in command.Content)
            {
                PdfRectangle characterBounds = characterMatrix.MapRect(character.BoundingBox);

                bounds = (bounds == null)
                    ? characterBounds
                    : PdfRectangle.Union(bounds.Value, characterBounds);
            }
        }
    }

    private static bool IsExcludedContentStream(PdfScope? scope)
    {
        return scope is PdfContentStreamScope contentStream
            && (contentStream.StreamType == PdfContentStreamType.SoftMask || contentStream.StreamType == PdfContentStreamType.Type3Glyph);
    }
}
