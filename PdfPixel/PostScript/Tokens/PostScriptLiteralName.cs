using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Literal name token (source had leading '/'). Pushed as a data object.
    /// </summary>
    public sealed class PostScriptLiteralName : PostScriptToken
    {
        public PostScriptLiteralName(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return "LiteralName: {\"" + Name + "\"}";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptLiteralName n && string.Equals(Name, n.Name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Name == null ?0 : StringComparer.Ordinal.GetHashCode(Name);
        }

        public override int CompareToToken(PostScriptToken other)
        {
            if (other is not PostScriptLiteralName n)
            {
                throw new InvalidOperationException("Literal name comparison requires name operand.");
            }
            return string.Compare(Name, n.Name, StringComparison.Ordinal);
        }
    }
}
