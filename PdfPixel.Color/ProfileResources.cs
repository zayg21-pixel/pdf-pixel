using PdfPixel.Color.Icc;
using System.IO;

namespace PdfPixel.Color;

/// <summary>
/// Provides predefined profile resources.
/// </summary>
public static class ProfileResources
{
    // CGATS001Compat-v2-micro.icc from Compact ICC Profiles: a 6-point A2B0 modeled on U.S. Web Coated (SWOP) v2,
    // plus a 17-point B2A0 generated with ArgyllCMS by inverting that A2B0.
    private const string CompactCmyk = "CompactCmyk.icc";
    private const string ResourcePrefix = $"{nameof(PdfPixel)}.{nameof(Color)}.EmbeddedData.";

    /// <summary>
    /// Returns predefined compact CMYK profile.
    /// </summary>
    /// <returns><see cref="IccProfile"/> instance.</returns>
    public static IccProfile GetCmykProfile()
    {
        using Stream? stream = typeof(ProfileResources).Assembly.GetManifestResourceStream($"{ResourcePrefix}{CompactCmyk}");

        if (stream == null)
        {
            throw new InvalidDataException("CompactCmyk is not defined");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);

        return IccProfile.Parse(memoryStream.ToArray());
    }
}
