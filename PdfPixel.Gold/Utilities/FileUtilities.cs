using System.Security.Cryptography;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Work on the names and bytes of corpus files, independent of their format.
/// </summary>
internal static class FileUtilities
{
    /// <summary>
    /// SHA-256 of the given bytes, lowercase hexadecimal as stored in the manifest.
    /// </summary>
    public static string ComputeSha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>
    /// The file name of the given PDF path or name, with ".pdf" appended when it has no such extension.
    /// </summary>
    public static string GetPdfFileName(string pdfPathOrName)
    {
        string fileName = Path.GetFileName(pdfPathOrName);

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }

        return fileName;
    }
}
