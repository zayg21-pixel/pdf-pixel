using System;

namespace PdfRender.PostScript.Tokens
{
    /// <summary>
    /// Numeric literal token.
    /// </summary>
    public sealed class PostScriptNumber : PostScriptToken
    {
        public PostScriptNumber(float value)
        {
            Value = value;
        }

        public float Value { get; }

        public override string ToString()
        {
            return "Number: " + Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptNumber n && Value.Equals(n.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override int CompareToToken(PostScriptToken other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is not PostScriptNumber n)
            {
                throw new InvalidOperationException("Numeric comparison requires numeric right operand.");
            }
            return Value.CompareTo(n.Value);
        }

        public override PostScriptToken LogicalAnd(PostScriptToken other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise AND requires numeric right operand.");
            }
            if (Value != (int)Value || right.Value != (int)right.Value)
            {
                throw new InvalidOperationException("Bitwise AND requires integral operands.");
            }
            int result = (int)Value & (int)right.Value;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken LogicalOr(PostScriptToken other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise OR requires numeric right operand.");
            }
            if (Value != (int)Value || right.Value != (int)right.Value)
            {
                throw new InvalidOperationException("Bitwise OR requires integral operands.");
            }
            int result = (int)Value | (int)right.Value;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken LogicalXor(PostScriptToken other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise XOR requires numeric right operand.");
            }
            if (Value != (int)Value || right.Value != (int)right.Value)
            {
                throw new InvalidOperationException("Bitwise XOR requires integral operands.");
            }
            int result = (int)Value ^ (int)right.Value;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken LogicalNot()
        {
            if (Value != (int)Value)
            {
                throw new InvalidOperationException("Bitwise NOT requires integral operand.");
            }
            int result = ~(int)Value;
            return new PostScriptNumber(result);
        }
    }
}
