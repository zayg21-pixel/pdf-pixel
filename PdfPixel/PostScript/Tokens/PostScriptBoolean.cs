using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Boolean literal token (true / false).
    /// </summary>
    public sealed class PostScriptBoolean : PostScriptToken
    {
        public PostScriptBoolean(bool value) => BooleanValue = value;

        public bool BooleanValue { get; }

        public override string ToString() => "Boolean: " + (BooleanValue ? "true" : "false");

        public override bool EqualsToken(PostScriptToken other) => other is PostScriptBoolean b && BooleanValue == b.BooleanValue;

        public override int GetHashCode() => BooleanValue.GetHashCode();

        // Booleans are not comparable with ordering operators in PostScript; keep defaults (false).

        public override PostScriptToken? LogicalAnd(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical AND requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue && right.BooleanValue);
        }

        public override PostScriptToken? LogicalOr(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical OR requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue || right.BooleanValue);
        }

        public override PostScriptToken? LogicalXor(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical XOR requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue ^ right.BooleanValue);
        }

        public override PostScriptToken LogicalNot() => new PostScriptBoolean(!BooleanValue);
    }
}
