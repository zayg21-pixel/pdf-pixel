using System;
using System.Text;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// String literal token.
    /// </summary>
    public sealed class PostScriptString : PostScriptToken
    {
        /// <summary>
        /// Initializes the string token wrapping the supplied byte array.
        /// </summary>
        /// <param name="value">The raw byte data representing the PostScript string contents.</param>
        public PostScriptString(byte[] value) => Data = value;

        /// <summary>
        /// Gets the raw byte data of this PostScript string.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Returns a diagnostic string showing the string content decoded as UTF-8.
        /// </summary>
        public override string ToString()
        {
            if (Data == null)
            {
                return "String: (null)";
            }

            return "String: \"" + Encoding.UTF8.GetString(Data) + "\"";
        }

        /// <summary>
        /// Returns true when <paramref name="other"/> is a string token with identical byte contents.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => CompareToToken(other) == 0;

        /// <summary>
        /// Returns a hash code computed from the individual bytes of the string data.
        /// </summary>
        public override int GetHashCode()
        {
            if (Data == null)
            {
                return 0;
            }

            HashCode hash = new();

            foreach (byte b in Data)
            {
                hash.Add(b);
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// Compares this string's bytes lexicographically to another string token's bytes.
        /// Throws if <paramref name="other"/> is not a string token.
        /// </summary>
        /// <param name="other">The token to compare against.</param>
        /// <returns>A negative value, zero, or a positive value indicating relative ordering.</returns>
        public override int CompareToToken(PostScriptToken? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is not PostScriptString otherString)
            {
                throw new InvalidOperationException("String comparison requires string operand.");
            }

            byte[] a = Data;
            byte[] b = otherString.Data;

            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return -1;
            }

            if (b == null)
            {
                return 1;
            }

            int minLength = Math.Min(a.Length, b.Length);

            for (int i = 0; i < minLength; i++)
            {
                int difference = a[i].CompareTo(b[i]);

                if (difference != 0)
                {
                    return difference;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        /// <summary>
        /// Returns the byte at the specified numeric index as a <see cref="PostScriptNumber"/>.
        /// Throws if the index is out of range or the key is not numeric.
        /// </summary>
        /// <param name="keyOrIndex">A <see cref="PostScriptNumber"/> token containing the zero-based index.</param>
        /// <returns>A <see cref="PostScriptNumber"/> holding the byte value at the specified position.</returns>
        public override PostScriptToken GetValue(PostScriptToken keyOrIndex)
        {
            EnsureAccess(PostScriptAccessOperation.Read);
            if (keyOrIndex is not PostScriptNumber number)
            {
                throw new InvalidOperationException("typecheck: string index must be number");
            }

            if (Data == null)
            {
                throw new InvalidOperationException("rangecheck: string is null");
            }

            var index = (int)number.Number;
            if (index < 0 || index >= Data.Length)
            {
                throw new InvalidOperationException("rangecheck: string index out of range");
            }

            int code = Data[index];
            return new PostScriptNumber(code);
        }

        /// <summary>
        /// Writes a byte value (0-255) at the specified numeric index within the string data.
        /// Throws if the key or value is not numeric, the index is out of range, or the replacement code is out of range.
        /// </summary>
        /// <param name="keyOrIndex">A <see cref="PostScriptNumber"/> token containing the zero-based index.</param>
        /// <param name="value">A <see cref="PostScriptNumber"/> token whose integer value (0-255) is written to the string.</param>
        public override void SetValue(PostScriptToken keyOrIndex, PostScriptToken value)
        {
            EnsureAccess(PostScriptAccessOperation.Modify);
            if (keyOrIndex is not PostScriptNumber number || value is not PostScriptNumber repl)
            {
                throw new InvalidOperationException("typecheck: string set expects numeric index and numeric value");
            }

            if (Data == null)
            {
                throw new InvalidOperationException("rangecheck: string is null");
            }

            var index = (int)number.Number;
            if (index < 0 || index >= Data.Length)
            {
                throw new InvalidOperationException("rangecheck: string index out of range");
            }

            float item = repl.Number;
            if (item < 0 || item > 255)
            {
                throw new InvalidOperationException("rangecheck: replacement code outside 0-255");
            }

            Data[index] = (byte)item;
        }
    }
}
