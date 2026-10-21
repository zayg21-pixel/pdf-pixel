using System.IO;

namespace PdfPixel.EmbeddedData;

/// <summary>
/// Loads embedded PDF resources from the assembly.
/// </summary>
public static class PdfResourceLoader
{
    /// <summary>
    /// Loads an embedded resource from the assembly as a byte array.
    /// </summary>
    /// <param name="resourceName">Resource name.</param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static byte[] GetResource(string resourceName)
    {
        System.Reflection.Assembly assembly = typeof(PdfResourceLoader).Assembly;
        // Open the resource stream
        using Stream? stream = assembly.GetManifestResourceStream($"{nameof(PdfPixel)}.{nameof(EmbeddedData)}.{resourceName}");

        if (stream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found.");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
