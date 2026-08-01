using System;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// A Type1 font program laid out as a single contiguous buffer: a cleartext header followed immediately
/// by its eexec-encrypted body. This is the layout a PFA-style embedding already has; a PFB-wrapped
/// embedding is reassembled into this same shape by <see cref="Type1PfbSegmentReader"/>.
/// </summary>
public readonly struct Type1RawFontProgram
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Type1RawFontProgram"/> struct.
    /// </summary>
    /// <param name="data">The contiguous buffer holding the cleartext header followed by the eexec-encrypted body.</param>
    /// <param name="length1">The length of the cleartext header at the start of <paramref name="data"/>.</param>
    /// <param name="length2">The length of the eexec-encrypted body immediately following the header in <paramref name="data"/>.</param>
    /// <param name="length3">The length of the fixed-content trailer (512 zeros plus <c>cleartomark</c>) immediately following the eexec-encrypted body, or 0 if unknown.</param>
    public Type1RawFontProgram(in ReadOnlyMemory<byte> data, int length1, int length2, int length3)
    {
        Data = data;
        Length1 = length1;
        Length2 = length2;
        Length3 = length3;
    }

    /// <summary>
    /// Gets the contiguous buffer holding the cleartext header followed by the eexec-encrypted body.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Gets the length of the cleartext header at the start of <see cref="Data"/>.
    /// </summary>
    public int Length1 { get; }

    /// <summary>
    /// Gets the length of the eexec-encrypted body immediately following the header in <see cref="Data"/>.
    /// </summary>
    public int Length2 { get; }

    /// <summary>
    /// Gets the length of the fixed-content trailer (512 zeros plus <c>cleartomark</c>) immediately
    /// following the eexec-encrypted body in <see cref="Data"/>, or 0 if unknown.
    /// </summary>
    public int Length3 { get; }
}
