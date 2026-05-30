using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Boolean literal token (true / false).
    /// </summary>
    public sealed class PostScriptBoolean : PostScriptToken
    {
        /// <summary>
        /// Initializes the boolean token with the specified value.
        /// </summary>
        /// <param name="value">The boolean value to represent.</param>
        public PostScriptBoolean(bool value) => BooleanValue = value;

        /// <summary>
        /// Gets the boolean value held by this token.
        /// </summary>
        public bool BooleanValue { get; }

        /// <summary>
        /// Returns a diagnostic string showing the boolean value as "true" or "false".
        /// </summary>
        public override string ToString() => "Boolean: " + (BooleanValue ? "true" : "false");

        /// <summary>
        /// Returns true when <paramref name="other"/> is a boolean token with the same value.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => other is PostScriptBoolean b && BooleanValue == b.BooleanValue;

        /// <summary>
        /// Returns a hash code based on the boolean value.
        /// </summary>
        public override int GetHashCode() => BooleanValue.GetHashCode();

        // Booleans are not comparable with ordering operators in PostScript; keep defaults (false).

        /// <summary>
        /// Applies the PostScript 'and' operator to two boolean operands, returning a new boolean token
        /// that is true only when both operands are true.
        /// </summary>
        /// <param name="other">The right-hand boolean operand.</param>
        /// <returns>A new <see cref="PostScriptBoolean"/> representing the logical AND result.</returns>
        public override PostScriptToken? LogicalAnd(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical AND requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue && right.BooleanValue);
        }

        /// <summary>
        /// Applies the PostScript 'or' operator to two boolean operands, returning a new boolean token
        /// that is true when either operand is true.
        /// </summary>
        /// <param name="other">The right-hand boolean operand.</param>
        /// <returns>A new <see cref="PostScriptBoolean"/> representing the logical OR result.</returns>
        public override PostScriptToken? LogicalOr(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical OR requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue || right.BooleanValue);
        }

        /// <summary>
        /// Applies the PostScript 'xor' operator to two boolean operands, returning a new boolean token
        /// that is true when the operands differ.
        /// </summary>
        /// <param name="other">The right-hand boolean operand.</param>
        /// <returns>A new <see cref="PostScriptBoolean"/> representing the logical XOR result.</returns>
        public override PostScriptToken? LogicalXor(PostScriptToken? other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical XOR requires boolean right operand.");
            }

            return new PostScriptBoolean(BooleanValue ^ right.BooleanValue);
        }

        /// <summary>
        /// Applies the PostScript 'not' operator, returning a new boolean token with the inverted value.
        /// </summary>
        /// <returns>A new <see cref="PostScriptBoolean"/> that is the logical negation of this token.</returns>
        public override PostScriptToken LogicalNot() => new PostScriptBoolean(!BooleanValue);
    }
}
