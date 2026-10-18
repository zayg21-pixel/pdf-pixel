using PdfPixel.Models;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Handle closing of an array. Validates matching frame and materializes PdfArray.
    /// </summary>
    /// <param name="values">Accumulated values list.</param>
    /// <param name="frames">Open collection frames stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleArrayEnd(List<PdfValue> values, Stack<CollectionFrame> frames)
    {
        if (frames.Count == 0 || frames.Peek().FrameToken != PdfTokenType.ArrayStart)
        {
            return;
        }

        CollectionFrame frame = frames.Pop();
        int itemCount = values.Count - frame.StartIndex;
        var items = new PdfValue[itemCount];

        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            items[itemIndex] = values[frame.StartIndex + itemIndex];
        }

        values.RemoveRange(frame.StartIndex, itemCount);
        PdfArray array = new(_document, items);
        values.Add(new PdfArrayValue(array));
    }

    /// <summary>
    /// Handle closing of a dictionary. Validates matching frame and materializes PdfDictionary.
    /// Builds raw key/value map first, then uses PdfDictionary(PdfDocument, Dictionary&lt;PdfString, PdfValue&gt;) overload.
    /// </summary>
    /// <param name="values">Accumulated values list.</param>
    /// <param name="frames">Open collection frames stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDictionaryEnd(List<PdfValue> values, Stack<CollectionFrame> frames)
    {
        if (frames.Count == 0 || frames.Peek().FrameToken != PdfTokenType.DictionaryStart)
        {
            return;
        }

        CollectionFrame frame = frames.Pop();
        int rawCount = values.Count - frame.StartIndex;
        Dictionary<PdfString, PdfValue> rawMap = [];

        int scanIndex = frame.StartIndex;
        while (scanIndex < values.Count)
        {
            PdfValue possibleKey = values[scanIndex];
            PdfString? keyName = possibleKey.AsName();
            if (keyName == null)
            {
                scanIndex++;
                continue;
            }

            int valueIndex = scanIndex + 1;
            if (valueIndex >= values.Count)
            {
                break;
            }

            PdfValue value = values[valueIndex];
            rawMap[keyName.Value] = value;
            scanIndex = valueIndex + 1;
        }

        values.RemoveRange(frame.StartIndex, rawCount);
        PdfDictionary dictionary = new(_document, rawMap);
        values.Add(new PdfDictionaryValue(dictionary));
    }
}
