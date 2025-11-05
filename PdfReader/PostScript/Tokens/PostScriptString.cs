using System;

namespace PdfReader.PostScript.Tokens
{
    /// <summary>
    /// String literal token.
    /// </summary>
    public sealed class PostScriptString : PostScriptToken
    {
        public PostScriptString(string value)
        {
            Value = value;
        }
        public string Value { get; private set; }

        public override string ToString()
        {
            if (Value == null)
            {
                return "String: (null)";
            }
            string escaped = Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "String: \"" + escaped + "\"";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptString s && string.Equals(Value, s.Value, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override int CompareToToken(PostScriptToken other)
        {
            if (other is not PostScriptString s)
            {
                throw new InvalidOperationException("String comparison requires string operand.");
            }
            return string.Compare(Value, s.Value, StringComparison.Ordinal);
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
            int codePoint = (int)repl.Value;
            if (codePoint < 0 || codePoint > 255)
            {
                throw new InvalidOperationException("rangecheck: replacement code outside0-255");
            }
            char replaceChar = (char)codePoint;
            char[] chars = Value.ToCharArray();
            chars[index] = replaceChar;
            Value = new string(chars);
        }
    }
}
