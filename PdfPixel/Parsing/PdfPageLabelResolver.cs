using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;
using System;
using System.Globalization;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves page labels from the /PageLabels number tree in the PDF catalog.
/// </summary>
public class PdfPageLabelResolver
{
    private readonly List<PageLabelEntry> _entries = [];

    internal PdfPageLabelResolver(PdfDictionary catalog)
    {
        PdfObject? pageLabelsObj = catalog.GetObject(PdfTokens.PageLabelsKey);
        if (pageLabelsObj == null)
        {
            return;
        }

        PdfDictionary numberTree = pageLabelsObj.Dictionary;
        PdfArray? nums = numberTree.GetValue(PdfTokens.NumsKey)?.AsArray();
        if (nums == null)
        {
            return;
        }

        for (int i = 0; i + 1 < nums.Count; i += 2)
        {
            int pageIndex = nums.GetIntegerOrDefault(i);
            PdfDictionary? labelDict = nums.GetObject(i + 1)?.Dictionary;
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
            return PdfString.FromString((pageIndex + 1).ToString(CultureInfo.CurrentCulture));
        }

        PageLabelEntry? current = null;
        foreach (PageLabelEntry entry in _entries)
        {
            if (entry.PageIndex > pageIndex)
            {
                break;
            }

            current = entry;
        }

        if (current == null)
        {
            return PdfString.FromString((pageIndex + 1).ToString(CultureInfo.CurrentCulture));
        }

        return FormatLabel(current.LabelDict, pageIndex - current.PageIndex);
    }

    private static PdfString FormatLabel(PdfDictionary labelDict, int index)
    {
        PdfString? prefix = labelDict.GetString(PdfTokens.PrefixKey);
        PageLabelStyle style = labelDict.GetNameOrDefault(PdfTokens.StyleKey).AsEnum<PageLabelStyle>();
        int start = labelDict.GetInteger(PdfTokens.StartKey) ?? 1;
        int number = start + index;
        PdfString numStr = style switch
        {
            PageLabelStyle.Decimal => PdfString.FromString(number.ToString(CultureInfo.CurrentCulture)),
            PageLabelStyle.LowerRoman => PdfString.FromString(ToRoman(number, false)),
            PageLabelStyle.UpperRoman => PdfString.FromString(ToRoman(number, true)),
            PageLabelStyle.LowerAlpha => PdfString.FromString(ToAlpha(number, false)),
            PageLabelStyle.UpperAlpha => PdfString.FromString(ToAlpha(number, true)),
            _ => PdfString.FromString(number.ToString(CultureInfo.CurrentCulture))
        };
        // Concatenate prefix and numStr at the byte level
        if (prefix == null)
        {
            return numStr;
        }

        if (numStr.IsEmpty)
        {
            return prefix.Value;
        }

        ReadOnlySpan<byte> prefixBytes = prefix.Value.Value.Span;
        ReadOnlySpan<byte> numBytes = numStr.Value.Span;
        var result = new byte[prefixBytes.Length + numBytes.Length];
        prefixBytes.CopyTo(result);
        numBytes.CopyTo(result.AsSpan().Slice(prefixBytes.Length));
        return new PdfString(result);
    }

    private static string ToRoman(int number, bool upper)
    {
        if (number <= 0)
        {
            return number.ToString(CultureInfo.CurrentCulture);
        }

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
            new { Value = 1, Numeral = "I" }
        };
        string result = string.Empty;
        foreach (var item in numerals)
        {
            while (number >= item.Value)
            {
                result += item.Numeral;
                number -= item.Value;
            }
        }

        return upper ? result : result.ToLower(CultureInfo.CurrentCulture);
    }

    private static string ToAlpha(int number, bool upper)
    {
        if (number <= 0)
        {
            return number.ToString(CultureInfo.CurrentCulture);
        }

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
