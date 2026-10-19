using PdfPixel.Parsing;
using System;
using System.IO;

namespace PdfPixel.Models;

/// <summary>
/// The <c>%PDF-</c> header of a file: where it starts and the version it declares.
/// </summary>
public sealed class PdfFileHeader
{
    private const int VersionLength = 8;

    private PdfFileHeader(int offset, PdfVersion? version)
    {
        Offset = offset;
        Version = version;
    }

    /// <summary>
    /// Absolute file position of the header. Offsets the file declares for itself (startxref,
    /// cross-reference entries) are relative to it.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Version the header declares, or null when it cannot be read.
    /// </summary>
    public PdfVersion? Version { get; }

    /// <summary>
    /// Locates the header in <paramref name="stream"/> and reads its version.
    /// </summary>
    /// <param name="stream">The document stream.</param>
    internal static PdfFileHeader FromStream(Stream stream)
    {
        int offset = PdfByteScanner.LocateHeader(stream);
        long versionPosition = offset + PdfByteScanner.Header.Length;
        if (versionPosition >= stream.Length)
        {
            return new PdfFileHeader(offset, null);
        }

        var versionBytes = new byte[VersionLength];
        long previousPosition = stream.Position;
        stream.Position = versionPosition;
        int bytesRead = stream.Read(versionBytes, 0, versionBytes.Length);
        stream.Position = previousPosition;

        return new PdfFileHeader(offset, PdfVersion.Parse(versionBytes.AsSpan(0, bytesRead)));
    }
}
