namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Parsed data from a CFF Top DICT.
/// </summary>
internal sealed class CffTopDictData
{
    public int? CharsetOffset { get; set; }

    public int? EncodingOffset { get; set; }

    public int? CharStringsOffset { get; set; }

    public int? PrivateDictSize { get; set; }

    public int? PrivateDictOffset { get; set; }

    public bool IsCidKeyed { get; set; }

    public decimal[] FontMatrix { get; set; }
}
