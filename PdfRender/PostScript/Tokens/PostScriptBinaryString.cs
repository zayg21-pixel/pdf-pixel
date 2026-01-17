using System;
using System.Runtime.CompilerServices;

namespace PdfRender.PostScript.Tokens
{
    /// <summary>
    /// Binary string token representing raw byte data (e.g. charstring / subr bodies) captured after RD/-|/|- operators.
    /// Immutable wrapper over byte[] to avoid accidental mutation.
    /// </summary>
    public sealed class PostScriptBinaryString : PostScriptToken
    {
        public PostScriptBinaryString(byte[] data)
        {
            Data = data ?? Array.Empty<byte>();
        }
        public byte[] Data { get; }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptBinaryString bin && ReferenceEquals(Data, bin.Data);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(Data);
        }

        public override string ToString()
        {
            return "BinaryString(length=" + Data.Length + ", access=" + AccessLevel + ")";
        }
    }
}
