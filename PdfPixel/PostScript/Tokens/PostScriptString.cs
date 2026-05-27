using System;
using System.Text;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// String literal token.
    /// </summary>
    public sealed class PostScriptString : PostScriptToken
    {
        public PostScriptString(byte[] value) => Data = value;

        public byte[] Data { get; }

        public override string ToString()
        {
            if (Data == null)
            {
                return "String: (null)";
            }

            return "String: \"" + Encoding.UTF8.GetString(Data) + "\"";
        }

        public override bool EqualsToken(PostScriptToken other) => CompareToToken(other) == 0;

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
