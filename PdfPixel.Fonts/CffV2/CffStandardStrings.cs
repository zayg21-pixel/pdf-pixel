using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// The CFF spec's predefined string tables: the 391 StandardStrings referenced by SID from DICTs,
/// and the three predefined charsets (ISOAdobe, Expert, ExpertSubset) that a name-keyed font may
/// reference instead of an explicit charset table.
/// </summary>
internal static class CffStandardStrings
{
    static CffStandardStrings()
    {
        StandardStrings = FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("StandardStrings.bin"));
        IsoAdobeStrings = FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("IsoAdobeStrings.bin"));
        ExpertStrings = FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("ExpertStrings.bin"));
        ExpertSubsetStrings = FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("ExpertSubsetStrings.bin"));
        StandardNameToSid = BuildStandardNameToSid();
    }

    public static readonly PdfFontString[] StandardStrings;
    public static readonly PdfFontString[] IsoAdobeStrings;
    public static readonly PdfFontString[] ExpertStrings;
    public static readonly PdfFontString[] ExpertSubsetStrings;

    /// <summary>
    /// Gets the reverse lookup of <see cref="StandardStrings"/>: SID by glyph name.
    /// </summary>
    public static readonly Dictionary<PdfFontString, ushort> StandardNameToSid;

    private static Dictionary<PdfFontString, ushort> BuildStandardNameToSid()
    {
        Dictionary<PdfFontString, ushort> standardNameToSid = new(StandardStrings.Length);
        for (ushort sid = 0; sid < StandardStrings.Length; sid++)
        {
            PdfFontString standardName = StandardStrings[sid];
            if (!standardName.IsEmpty && !standardNameToSid.ContainsKey(standardName))
            {
                standardNameToSid[standardName] = sid;
            }
        }

        return standardNameToSid;
    }
}
