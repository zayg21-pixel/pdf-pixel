using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Numeric literal token.
    /// </summary>
    public sealed class PostScriptNumber : PostScriptToken
    {
        public PostScriptNumber(float value) => Number = value;

        public float Number { get; }

        public override string ToString() => "Number: " + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public override bool EqualsToken(PostScriptToken other) => other is PostScriptNumber n && Number.Equals(n.Number);

        public override int GetHashCode() => Number.GetHashCode();

        public override int CompareToToken(PostScriptToken? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is not PostScriptNumber n)
            {
                throw new InvalidOperationException("Numeric comparison requires numeric right operand.");
            }

            return Number.CompareTo(n.Number);
        }

        public override PostScriptToken? LogicalAnd(PostScriptToken? other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise AND requires numeric right operand.");
            }

            if (Number != (int)Number || right.Number != (int)right.Number)
            {
                throw new InvalidOperationException("Bitwise AND requires integral operands.");
            }

            int result = (int)Number & (int)right.Number;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken? LogicalOr(PostScriptToken? other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise OR requires numeric right operand.");
            }

            if (Number != (int)Number || right.Number != (int)right.Number)
            {
                throw new InvalidOperationException("Bitwise OR requires integral operands.");
            }

            int result = (int)Number | (int)right.Number;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken? LogicalXor(PostScriptToken? other)
        {
            if (other is not PostScriptNumber right)
            {
                throw new InvalidOperationException("Bitwise XOR requires numeric right operand.");
            }

            if (Number != (int)Number || right.Number != (int)right.Number)
            {
                throw new InvalidOperationException("Bitwise XOR requires integral operands.");
            }

            int result = (int)Number ^ (int)right.Number;
            return new PostScriptNumber(result);
        }

        public override PostScriptToken LogicalNot()
        {
            if (Number != (int)Number)
            {
                throw new InvalidOperationException("Bitwise NOT requires integral operand.");
            }

            int result = ~(int)Number;
            return new PostScriptNumber(result);
        }
    }
}
