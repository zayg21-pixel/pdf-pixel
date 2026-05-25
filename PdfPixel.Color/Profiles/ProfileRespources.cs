using PdfPixel.Color.Icc.Model;
using System.IO;

namespace PdfPixel.Color.Profiles;

/// <summary>
/// Provides predefined profile resources.
/// </summary>
public static class ProfileRespources
{
    private const string CompactCmyk = "CompactCmyk.icc";
    private const string ResourcePrefix = $"{nameof(PdfPixel)}.{nameof(Color)}.{nameof(Profiles)}.";

    /// <summary>
    /// Returns predefined compact CMYK profile.
    /// </summary>
    /// <returns></returns>
    public static IccProfile GetCmykProfile()
    {
        using Stream? stream = typeof(ProfileRespources).Assembly.GetManifestResourceStream($"{ResourcePrefix}{CompactCmyk}");

        if (stream == null)
        {
            throw new InvalidDataException("CompactCmyk is not defined");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);

        return IccProfile.Parse(memoryStream.ToArray());
    }
}
