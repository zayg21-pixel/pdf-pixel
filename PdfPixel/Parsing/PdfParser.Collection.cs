using PdfPixel.Models;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

partial struct PdfParser
{
    /// <summary>
    /// Handle closing of an array. Validates matching frame and materializes PdfArray.
    /// </summary>
    /// <param name="values">Accumulated values list.</param>
    /// <param name="frames">Open collection frames stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleArrayEnd(List<IPdfValue> values, Stack<CollectionFrame> frames)
    {
        if (frames.Count == 0 || frames.Peek().FrameToken != PdfTokenType.ArrayStart)
        {
            return;
        }

        CollectionFrame frame = frames.Pop();
        int itemCount = values.Count - frame.StartIndex;
        IPdfValue[] items = new IPdfValue[itemCount];

        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            items[itemIndex] = values[frame.StartIndex + itemIndex];
        }

        values.RemoveRange(frame.StartIndex, itemCount);
        var array = new PdfArray(_document, items);
        values.Add(PdfValueFactory.Array(array));
    }

    /// <summary>
    /// Handle closing of a dictionary. Validates matching frame and materializes PdfDictionary.
    /// Builds raw key/value map first, then uses PdfDictionary(PdfDocument, Dictionary<PdfString, IPdfValue>) overload.
    /// </summary>
    /// <param name="values">Accumulated values list.</param>
    /// <param name="frames">Open collection frames stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDictionaryEnd(List<IPdfValue> values, Stack<CollectionFrame> frames)
    {
        if (frames.Count == 0 || frames.Peek().FrameToken != PdfTokenType.DictionaryStart)
        {
            return;
        }

        CollectionFrame frame = frames.Pop();
        int rawCount = values.Count - frame.StartIndex;
        var rawMap = new Dictionary<PdfString, IPdfValue>();

        int scanIndex = frame.StartIndex;
        while (scanIndex < values.Count)
        {
            IPdfValue possibleKey = values[scanIndex];
            PdfString keyName = possibleKey.AsName();
            if (keyName.IsEmpty)
            {
                scanIndex++;
                continue;
            }

            int valueIndex = scanIndex + 1;
            if (valueIndex >= values.Count)
            {
                break;
            }

            IPdfValue value = values[valueIndex];
            rawMap[keyName] = value;
            scanIndex = valueIndex + 1;
        }

        values.RemoveRange(frame.StartIndex, rawCount);
        var dictionary = new PdfDictionary(_document, rawMap);
        values.Add(PdfValueFactory.Dictionary(dictionary));
    }
}
