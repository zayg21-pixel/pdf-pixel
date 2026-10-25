using PdfPixel.Color.Icc;
using System.IO;

namespace PdfPixel.Color;

/// <summary>
/// Provides predefined profile resources.
/// </summary>
public static class ProfileResources
{
    // The perceptual mapping is modeled on the appearance of Adobe's U.S. Web Coated (SWOP) v2 profile,
    // which is the long-time Photoshop default for North American users -- though it is not intended to be a direct replacement for the Adobe profile.
    // Due to the small size of the 4D LUT, this profile will not match the Adobe profile's output exactly,
    // but it should give a correct overall appearance to an image when converted for display.
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
