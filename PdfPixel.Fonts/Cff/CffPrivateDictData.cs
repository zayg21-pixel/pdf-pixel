namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Parsed data from a CFF Private DICT.
/// </summary>
internal sealed class CffPrivateDictData
{
    public double? DefaultWidthX { get; set; }

    public double? NominalWidthX { get; set; }

    public int? SubrsOffset { get; set; }
}
