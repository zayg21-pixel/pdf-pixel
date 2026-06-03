using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// Binary string token representing raw byte data (e.g. charstring / subr bodies) captured after RD/-|/|- operators.
/// Immutable wrapper over byte[] to avoid accidental mutation.
/// </summary>
public sealed class PostScriptBinaryString : PostScriptToken
{
    /// <summary>
    /// Initializes the binary string with the supplied byte array. A null argument is treated as an empty array.
    /// </summary>
    /// <param name="data">The raw byte data to wrap.</param>
    public PostScriptBinaryString(byte[] data) => Data = data ?? Array.Empty<byte>();

    /// <summary>
    /// Gets the raw byte data stored in this binary string.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Returns true only when <paramref name="other"/> wraps the same <see cref="Data"/> array instance.
    /// </summary>
    public override bool EqualsToken(PostScriptToken other) => other is PostScriptBinaryString bin && ReferenceEquals(Data, bin.Data);

    /// <summary>
    /// Returns a hash code based on the identity of the underlying byte array.
    /// </summary>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Data);

    /// <summary>
    /// Returns a diagnostic string showing the byte length and current access level.
    /// </summary>
    public override string ToString() => "BinaryString(length=" + Data.Length + ", access=" + AccessLevel + ")";
}
