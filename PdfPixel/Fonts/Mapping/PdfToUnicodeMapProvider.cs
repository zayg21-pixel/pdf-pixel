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
    private static readonly Lazy<Dictionary<uint, string>> _japanToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeJapanEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _cnsToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeCnsEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _gbToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeGbEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _koreaToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeKoreaEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _krToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeKrEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    private static readonly Lazy<Dictionary<uint, string>> _mangaToUnicode = new Lazy<Dictionary<uint, string>>(() =>
    {
        var resource = PdfResourceLoader.GetResource("AdobeMangaEncodings.bin");
        var dict = new Dictionary<uint, string>();
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    });

    /// <summary>
    /// Returns a ToUnicode dictionary for the given CID system info, or null if not available.
    /// </summary>
    /// <param name="cidSystemInfo">CID system info (must not be null).</param>
    /// <returns>ToUnicode dictionary, or null if not available.</returns>
    public static Dictionary<uint, string> GetToUnicodeMap(PdfCidSystemInfo cidSystemInfo)
    {
        if (cidSystemInfo == null)
        {
            return null;
        }

        var ordering = cidSystemInfo.Ordering.Value.Span;

        var japanPrefix = "Japan"u8;

        if (ordering.Length >= japanPrefix.Length && ordering.Slice(0, japanPrefix.Length).SequenceEqual(japanPrefix))
        {
            return _japanToUnicode.Value;
        }

        var cnsPrefix = "CNS"u8;

        if (ordering.Length >= cnsPrefix.Length && ordering.Slice(0, cnsPrefix.Length).SequenceEqual(cnsPrefix))
        {
            return _cnsToUnicode.Value;
        }

        var gbPrefix = "GB"u8;

        if (ordering.Length >= gbPrefix.Length && ordering.Slice(0, gbPrefix.Length).SequenceEqual(gbPrefix))
        {
            return _gbToUnicode.Value;
        }

        var koreaPrefix = "Korea"u8;

        if (ordering.Length >= koreaPrefix.Length && ordering.Slice(0, koreaPrefix.Length).SequenceEqual(koreaPrefix))
        {
            return _koreaToUnicode.Value;
        }

        var krPrefix = "KR"u8;

        if (ordering.Length >= krPrefix.Length && ordering.Slice(0, krPrefix.Length).SequenceEqual(krPrefix))
        {
            return _krToUnicode.Value;
        }

        var mangaPrevix = "Manga"u8;

        if (ordering.Length >= mangaPrevix.Length && ordering.Slice(0, mangaPrevix.Length).SequenceEqual(mangaPrevix))
        {
            return _mangaToUnicode.Value;
        }

        return null;
    }
}
