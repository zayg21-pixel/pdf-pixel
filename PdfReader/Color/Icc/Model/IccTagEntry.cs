using PdfReader.Color.Icc.Utilities;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Represents a single ICC tag directory entry (signature, file offset and byte size).
/// The directory is parsed from the profile header region and each entry is later decoded
/// selectively depending on the tag's relevance for PDF color conversion.
/// </summary>
internal sealed class IccTagEntry
{
    /// <summary>
    /// Create a new tag directory entry.
    /// </summary>
    /// <param name="signature">Four-character code identifying the tag (stored as big-endian uInt32).</param>
    /// <param name="offset">Byte offset of the tag data relative to the start of the profile.</param>
    /// <param name="size">Length of the tag data in bytes.</param>
    public IccTagEntry(uint signature, int offset, int size)
    {
        Signature = signature;
        Offset = offset;
        Size = size;
    }

    /// <summary>
    /// Raw 32-bit big-endian signature (FourCC) of the tag.
    /// </summary>
    public uint Signature { get; }

    /// <summary>
    /// Byte offset (from profile start) where the tag data block begins.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Size in bytes of the tag data block.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Convenience string representation of the signature (4 printable ASCII characters when valid).
    /// </summary>
    public string SignatureString => BigEndianReader.FourCCToString(Signature);
}
