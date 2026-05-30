using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Numeric literal token.
    /// </summary>
    public sealed class PostScriptNumber : PostScriptToken
    {
        /// <summary>
        /// Initializes the numeric token with the specified floating-point value.
        /// </summary>
        /// <param name="value">The numeric value to represent.</param>
        public PostScriptNumber(float value) => Number = value;

        /// <summary>
        /// Gets the floating-point value held by this token. Integer values are stored as whole-number floats.
        /// </summary>
        public float Number { get; }

        /// <summary>
        /// Returns a diagnostic string showing the numeric value in invariant culture format.
        /// </summary>
        public override string ToString() => "Number: " + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns true when <paramref name="other"/> is a numeric token with an equal floating-point value.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => other is PostScriptNumber n && Number.Equals(n.Number);

        /// <summary>
        /// Returns a hash code derived from the numeric value.
        /// </summary>
        public override int GetHashCode() => Number.GetHashCode();

        /// <summary>
        /// Compares this token's value to another numeric token's value. Throws if <paramref name="other"/> is not a numeric token.
        /// </summary>
        /// <param name="other">The token to compare against.</param>
        /// <returns>A negative value, zero, or a positive value indicating relative ordering.</returns>
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

        /// <summary>
        /// Applies the PostScript 'and' operator to two integral numeric operands using bitwise AND.
        /// Both operands must have whole-number values; throws otherwise.
        /// </summary>
        /// <param name="other">The right-hand integral numeric operand.</param>
        /// <returns>A new <see cref="PostScriptNumber"/> containing the bitwise AND result.</returns>
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

        /// <summary>
        /// Applies the PostScript 'or' operator to two integral numeric operands using bitwise OR.
        /// Both operands must have whole-number values; throws otherwise.
        /// </summary>
        /// <param name="other">The right-hand integral numeric operand.</param>
        /// <returns>A new <see cref="PostScriptNumber"/> containing the bitwise OR result.</returns>
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

        /// <summary>
        /// Applies the PostScript 'xor' operator to two integral numeric operands using bitwise XOR.
        /// Both operands must have whole-number values; throws otherwise.
        /// </summary>
        /// <param name="other">The right-hand integral numeric operand.</param>
        /// <returns>A new <see cref="PostScriptNumber"/> containing the bitwise XOR result.</returns>
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

        /// <summary>
        /// Applies the PostScript 'not' operator, returning the bitwise complement of this integral numeric value.
        /// The value must have a whole-number value; throws otherwise.
        /// </summary>
        /// <returns>A new <see cref="PostScriptNumber"/> containing the bitwise NOT result.</returns>
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
