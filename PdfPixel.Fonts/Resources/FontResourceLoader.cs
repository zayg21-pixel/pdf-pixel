using System.IO;

namespace PdfPixel.Fonts.Resources;

/// <summary>
/// Loads embedded font resources from the assembly.
/// </summary>
internal static class FontResourceLoader
{
    /// <summary>
    /// Loads an embedded resource from the assembly as a byte array.
    /// </summary>
    /// <param name="resourceName">Resource name.</param>
    /// <exception cref="FileNotFoundException">Thrown when the resource is not found.</exception>
    public static byte[] GetResource(string resourceName)
    {
        System.Reflection.Assembly assembly = typeof(FontResourceLoader).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream($"{nameof(PdfPixel)}.{nameof(Fonts)}.{nameof(Resources)}.{resourceName}");

        if (stream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found.");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
