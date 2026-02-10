using System;
using System.Text;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// String literal token.
    /// </summary>
    public sealed class PostScriptString : PostScriptToken
    {
        public PostScriptString(byte[] value)
        {
            Value = value;
        }

        public byte[] Value { get; }

        public override string ToString()
        {
            if (Value == null)
            {
                return "String: (null)";
            }
            return "String: \"" + Encoding.UTF8.GetString(Value) + "\"";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return CompareToToken(other) == 0;
        }

        public override int GetHashCode()
        {
            if (Value == null)
            {
                return 0;
            }
            var hash = new HashCode();

            foreach (var b in Value)
            {
                hash.Add(b);
            }

            return hash.ToHashCode();
        }

        public override int CompareToToken(PostScriptToken other)
        {
            if (other is null)
            {
                return 1;
            }

            if (other is not PostScriptString otherString)
            {
                throw new InvalidOperationException("String comparison requires string operand.");
            }

            byte[] a = Value;
            byte[] b = otherString.Value;

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
            if (Value == null)
            {
                throw new InvalidOperationException("rangecheck: string is null");
            }
            int index = (int)number.Value;
            if (index < 0 || index >= Value.Length)
            {
                throw new InvalidOperationException("rangecheck: string index out of range");
            }
            int code = Value[index];
            return new PostScriptNumber(code);
        }

        public override void SetValue(PostScriptToken keyOrIndex, PostScriptToken value)
        {
            EnsureAccess(PostScriptAccessOperation.Modify);
            if (keyOrIndex is not PostScriptNumber number || value is not PostScriptNumber repl)
            {
                throw new InvalidOperationException("typecheck: string set expects numeric index and numeric value");
            }
            if (Value == null)
            {
                throw new InvalidOperationException("rangecheck: string is null");
            }
            int index = (int)number.Value;
            if (index < 0 || index >= Value.Length)
            {
                throw new InvalidOperationException("rangecheck: string index out of range");
            }
            var item = repl.Value;
            if (item < 0 || item > 255)
            {
                throw new InvalidOperationException("rangecheck: replacement code outside 0-255");
            }
            Value[index] = (byte)item;
        }
    }
}
