using System;

namespace PdfReader.PostScript.Tokens
{
    /// <summary>
    /// Boolean literal token (true / false).
    /// </summary>
    public sealed class PostScriptBoolean : PostScriptToken
    {
        public PostScriptBoolean(bool value)
        {
            Value = value;
        }

        public bool Value { get; }

        public override string ToString()
        {
            return "Boolean: " + (Value ? "true" : "false");
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptBoolean b && Value == b.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        // Booleans are not comparable with ordering operators in PostScript; keep defaults (false).

        public override PostScriptToken LogicalAnd(PostScriptToken other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical AND requires boolean right operand.");
            }
            return new PostScriptBoolean(Value & right.Value);
        }
        public override PostScriptToken LogicalOr(PostScriptToken other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical OR requires boolean right operand.");
            }
            return new PostScriptBoolean(Value | right.Value);
        }

        public override PostScriptToken LogicalXor(PostScriptToken other)
        {
            if (other is not PostScriptBoolean right)
            {
                throw new InvalidOperationException("Logical XOR requires boolean right operand.");
            }
            return new PostScriptBoolean(Value ^ right.Value);
        }

        public override PostScriptToken LogicalNot()
        {
            return new PostScriptBoolean(!Value);
        }
    }
}
