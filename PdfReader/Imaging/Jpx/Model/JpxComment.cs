namespace PdfReader.Imaging.Jpx.Model;

/// <summary>
/// Represents a comment from COM marker segment.
/// </summary>
internal sealed class JpxComment
{
    /// <summary>
    /// Gets or sets the registration value (0 = binary, 1 = Latin-1).
    /// </summary>
    public ushort Registration { get; set; }

    /// <summary>
    /// Gets or sets the comment data.
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// Gets a value indicating whether this comment is binary (registration = 0).
    /// </summary>
    public bool IsBinary => Registration == 0;

    /// <summary>
    /// Gets a value indicating whether this comment is Latin-1 text (registration = 1).
    /// </summary>
    public bool IsLatin1Text => Registration == 1;
}