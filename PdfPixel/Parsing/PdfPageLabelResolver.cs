using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves page labels from the /PageLabels number tree in the PDF catalog.
/// </summary>
public class PdfPageLabelResolver
{
    private static readonly int[] RomanValues = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
    private static readonly string[] UpperRomanNumerals = ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];
    private static readonly string[] LowerRomanNumerals = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];

    private readonly Dictionary<int, PdfDictionary> _labels = [];
    private readonly int[] _startPageIndices = Array.Empty<int>();

    internal PdfPageLabelResolver(PdfDictionary catalog, PdfTreeReader treeReader)
    {
        PdfDictionary? numberTree = catalog.GetDictionary(PdfTokens.PageLabelsKey);
        if (numberTree == null)
        {
            return;
        }

        _labels = treeReader.ReadNumberTree(numberTree, ReadLabel);
        _startPageIndices = new int[_labels.Count];
        _labels.Keys.CopyTo(_startPageIndices, 0);
        Array.Sort(_startPageIndices);
    }

    /// <summary>
    /// Gets the label for the given 0-based page index.
    /// </summary>
    public PdfString GetLabel(int pageIndex)
    {
        int position = Array.BinarySearch(_startPageIndices, pageIndex);
        if (position < 0)
        {
            position = ~position - 1;
        }

        if (position < 0)
        {
            return PdfString.FromString((pageIndex + 1).ToString(CultureInfo.InvariantCulture));
        }

        int startPageIndex = _startPageIndices[position];
        return FormatLabel(_labels[startPageIndex], pageIndex - startPageIndex);
    }

    private static PdfDictionary? ReadLabel(PdfArray numbers, int index) => numbers.GetDictionary(index);

    private static PdfString FormatLabel(PdfDictionary labelDictionary, int offset)
    {
        PdfString? prefix = labelDictionary.GetString(PdfTokens.PrefixKey);
        PageLabelStyle? style = labelDictionary.GetName(PdfTokens.StyleKey)?.AsEnum<PageLabelStyle>();
        if (style == null || style == PageLabelStyle.Unknown)
        {
            return prefix ?? PdfString.Empty;
        }

        int start = labelDictionary.GetInteger(PdfTokens.StartKey) ?? 1;
        int number = start + offset;

        PdfString numberText = style switch
        {
            PageLabelStyle.LowerRoman => PdfString.FromString(ToRoman(number, uppercase: false)),
            PageLabelStyle.UpperRoman => PdfString.FromString(ToRoman(number, uppercase: true)),
            PageLabelStyle.LowerAlpha => PdfString.FromString(ToAlpha(number, uppercase: false)),
            PageLabelStyle.UpperAlpha => PdfString.FromString(ToAlpha(number, uppercase: true)),
            _ => PdfString.FromString(number.ToString(CultureInfo.InvariantCulture))
        };

        if (prefix == null)
        {
            return numberText;
        }

        if (numberText.IsEmpty)
        {
            return prefix.Value;
        }

        ReadOnlySpan<byte> prefixBytes = prefix.Value.Value.Span;
        ReadOnlySpan<byte> numberBytes = numberText.Value.Span;
        var label = new byte[prefixBytes.Length + numberBytes.Length];
        prefixBytes.CopyTo(label);
        numberBytes.CopyTo(label.AsSpan(prefixBytes.Length));

        return new PdfString(label);
    }

    private static string ToRoman(int number, bool uppercase)
    {
        if (number <= 0)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        string[] numerals = uppercase ? UpperRomanNumerals : LowerRomanNumerals;
        StringBuilder roman = new();
        int remaining = number;

        for (int index = 0; index < RomanValues.Length; index++)
        {
            while (remaining >= RomanValues[index])
            {
                roman.Append(numerals[index]);
                remaining -= RomanValues[index];
            }
        }

        return roman.ToString();
    }

    private static string ToAlpha(int number, bool uppercase)
    {
        if (number <= 0)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        char firstLetter = uppercase ? 'A' : 'a';
        var letter = (char)(firstLetter + ((number - 1) % 26));
        int repeatCount = ((number - 1) / 26) + 1;

        return new string(letter, repeatCount);
    }
}
