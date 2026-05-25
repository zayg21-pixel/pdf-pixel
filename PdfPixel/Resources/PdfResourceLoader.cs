using PdfPixel.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PdfPixel.Resources;

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
        using Stream stream = assembly.GetManifestResourceStream($"PdfPixel.Resources.{resourceName}");

        if (stream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found.");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Loads a zip-compressed embedded resource from the assembly as a byte array.
    /// </summary>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="catalogPath">Path to the catalog within the zip.</param>
    /// <returns>Decompressed bytes.</returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static byte[] GetZipCompressedResource(string resourceName, string catalogPath)
    {
        System.Reflection.Assembly assembly = typeof(PdfTextResourceConverter).Assembly;
        using Stream compressedStream = assembly.GetManifestResourceStream($"PdfPixel.Resources.{resourceName}");

        if (compressedStream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found.");
        }

        using ZipArchive zipArchive = new(compressedStream);
        ZipArchiveEntry entry = zipArchive.Entries.FirstOrDefault(e => string.Equals(e.FullName, catalogPath, System.StringComparison.OrdinalIgnoreCase));

        using Stream entryStream = entry.Open();
        using MemoryStream decompressedStream = new();
        entryStream.CopyTo(decompressedStream);
        return decompressedStream.ToArray();
    }
}
