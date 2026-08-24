using PdfPixel.Fonts.Model;
using PdfPixel.Resources;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides access to built-in ToUnicode mappings for known CIDSystemInfo values.
/// </summary>
public static class PdfToUnicodeMapProvider
{
    private static readonly Lazy<Dictionary<uint, string>> _japanToUnicode = new(() =>
    {
        byte[] resource = PdfResourceLoader.GetResource("External.CidToUnicode.Adobe-Japan1-UCS2.bin");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _cnsToUnicode = new(() =>
    {
        byte[] resource = PdfResourceLoader.GetResource("External.CidToUnicode.Adobe-CNS1-UCS2.bin");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _gbToUnicode = new(() =>
    {
        byte[] resource = PdfResourceLoader.GetResource("External.CidToUnicode.Adobe-GB1-UCS2.bin");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _koreaToUnicode = new(() =>
    {
        byte[] resource = PdfResourceLoader.GetResource("External.CidToUnicode.Adobe-Korea1-UCS2.bin");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _krToUnicode = new(() =>
    {
        byte[] resource = PdfResourceLoader.GetResource("External.CidToUnicode.Adobe-KR-UCS2.bin");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    /// <summary>
    /// Returns a ToUnicode dictionary for the given CID system info, or null if not available.
    /// </summary>
    /// <param name="cidSystemInfo">CID system info (must not be null).</param>
    /// <returns>ToUnicode dictionary, or null if not available.</returns>
    public static Dictionary<uint, string>? GetToUnicodeMap(PdfCidSystemInfo? cidSystemInfo)
    {
        if (cidSystemInfo == null)
        {
            return null;
        }

        if (cidSystemInfo.Ordering == null)
        {
            return null;
        }

        ReadOnlySpan<byte> ordering = cidSystemInfo.Ordering.Value.Value.Span;

        ReadOnlySpan<byte> japanPrefix = "Japan"u8;
        if (ordering.Length >= japanPrefix.Length && ordering.Slice(0, japanPrefix.Length).SequenceEqual(japanPrefix))
        {
            return _japanToUnicode.Value;
        }

        ReadOnlySpan<byte> cnsPrefix = "CNS"u8;
        if (ordering.Length >= cnsPrefix.Length && ordering.Slice(0, cnsPrefix.Length).SequenceEqual(cnsPrefix))
        {
            return _cnsToUnicode.Value;
        }

        ReadOnlySpan<byte> gbPrefix = "GB"u8;
        if (ordering.Length >= gbPrefix.Length && ordering.Slice(0, gbPrefix.Length).SequenceEqual(gbPrefix))
        {
            return _gbToUnicode.Value;
        }

        ReadOnlySpan<byte> koreaPrefix = "Korea"u8;
        if (ordering.Length >= koreaPrefix.Length && ordering.Slice(0, koreaPrefix.Length).SequenceEqual(koreaPrefix))
        {
            return _koreaToUnicode.Value;
        }

        ReadOnlySpan<byte> krPrefix = "KR"u8;
        if (ordering.Length >= krPrefix.Length && ordering.Slice(0, krPrefix.Length).SequenceEqual(krPrefix))
        {
            return _krToUnicode.Value;
        }

        return null;
    }
}
