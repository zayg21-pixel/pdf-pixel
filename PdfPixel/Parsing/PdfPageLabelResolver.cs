using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;
using System.Buffers;
using System;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves page labels from the /PageLabels number tree in the PDF catalog.
/// </summary>
public class PdfPageLabelResolver
{
    private readonly List<PageLabelEntry> _entries = new();

    public PdfPageLabelResolver(PdfDictionary catalog)
    {
        var pageLabelsObj = catalog.GetObject(PdfTokens.PageLabelsKey);
        if (pageLabelsObj == null)
        {
            return;
        }
        var numberTree = pageLabelsObj.Dictionary;
        var nums = numberTree.GetValue(PdfTokens.NumsKey)?.AsArray();
        if (nums == null)
        {
            return;
        }
        for (int i = 0; i + 1 < nums.Count; i += 2)
        {
            int pageIndex = nums.GetIntegerOrDefault(i);
            var labelDict = nums.GetObject(i + 1)?.Dictionary;
            if (labelDict != null)
            {
                _entries.Add(new PageLabelEntry(pageIndex, labelDict));
            }
        }
        _entries.Sort((a, b) => a.PageIndex.CompareTo(b.PageIndex));
    }

    /// <summary>
    /// Gets the label for the given 0-based page index.
    /// </summary>
    public PdfString GetLabel(int pageIndex)
    {
        if (_entries.Count == 0)
        {
            return PdfString.FromString((pageIndex + 1).ToString());
        }
        PageLabelEntry current = null;
        foreach (var entry in _entries)
        {
            if (entry.PageIndex > pageIndex)
            {
                break;
            }
            current = entry;
        }
        if (current == null)
        {
            return PdfString.FromString((pageIndex + 1).ToString());
        }
        return FormatLabel(current.LabelDict, pageIndex - current.PageIndex);
    }

    private static PdfString FormatLabel(PdfDictionary labelDict, int index)
    {
        PdfString prefix = labelDict.GetString(PdfTokens.PrefixKey);
        PdfString styleString = labelDict.GetName(PdfTokens.StyleKey);
        var style = styleString.AsEnum<PageLabelStyle>();
        int start = labelDict.GetInteger(PdfTokens.StartKey) ?? 1;
        int number = start + index;
        PdfString numStr = style switch
        {
            PageLabelStyle.Decimal => PdfString.FromString(number.ToString()),
            PageLabelStyle.LowerRoman => PdfString.FromString(ToRoman(number, false)),
            PageLabelStyle.UpperRoman => PdfString.FromString(ToRoman(number, true)),
            PageLabelStyle.LowerAlpha => PdfString.FromString(ToAlpha(number, false)),
            PageLabelStyle.UpperAlpha => PdfString.FromString(ToAlpha(number, true)),
            _ => PdfString.FromString(number.ToString()),
        };
        // Concatenate prefix and numStr at the byte level
        if (prefix.IsEmpty)
        {
            return numStr;
        }

        if (numStr.IsEmpty)
        {
            return prefix;
        }

        var prefixBytes = prefix.Value.Span;
        var numBytes = numStr.Value.Span;
        byte[] result = new byte[prefixBytes.Length + numBytes.Length];
        prefixBytes.CopyTo(result);
        numBytes.CopyTo(result.AsSpan().Slice(prefixBytes.Length));
        return new PdfString(result);
    }

    private static string ToRoman(int number, bool upper)
    {
        if (number <= 0) return number.ToString();
        var numerals = new[]
        {
            new { Value = 1000, Numeral = "M" },
            new { Value = 900, Numeral = "CM" },
            new { Value = 500, Numeral = "D" },
            new { Value = 400, Numeral = "CD" },
            new { Value = 100, Numeral = "C" },
            new { Value = 90, Numeral = "XC" },
            new { Value = 50, Numeral = "L" },
            new { Value = 40, Numeral = "XL" },
            new { Value = 10, Numeral = "X" },
            new { Value = 9, Numeral = "IX" },
            new { Value = 5, Numeral = "V" },
            new { Value = 4, Numeral = "IV" },
            new { Value = 1, Numeral = "I" },
        };
        var result = string.Empty;
        foreach (var item in numerals)
        {
            while (number >= item.Value)
            {
                result += item.Numeral;
                number -= item.Value;
            }
        }
        return upper ? result : result.ToLowerInvariant();
    }

    private static string ToAlpha(int number, bool upper)
    {
        if (number <= 0) return number.ToString();
        string result = string.Empty;
        int n = number;
        while (n > 0)
        {
            n--;
            result = (char)((upper ? 'A' : 'a') + (n % 26)) + result;
            n /= 26;
        }
        return result;
    }

    private class PageLabelEntry
    {
        public int PageIndex { get; }
        public PdfDictionary LabelDict { get; }
        public PageLabelEntry(int pageIndex, PdfDictionary labelDict)
        {
            PageIndex = pageIndex;
            LabelDict = labelDict;
        }
    }
}
