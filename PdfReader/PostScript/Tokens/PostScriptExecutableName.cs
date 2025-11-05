using System;

namespace PdfReader.PostScript.Tokens
{
    /// <summary>
    /// Executable name token (bare name without leading slash). Subject to dictionary / builtin lookup.
    /// </summary>
    public sealed class PostScriptExecutableName : PostScriptToken
    {
        public PostScriptExecutableName(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return "ExecutableName: {" + Name + "}";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return other is PostScriptExecutableName n && string.Equals(Name, n.Name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Name == null ?0 : StringComparer.Ordinal.GetHashCode(Name);
        }

        public override int CompareToToken(PostScriptToken other)
        {
            if (other is not PostScriptExecutableName n)
            {
                throw new InvalidOperationException("Name comparison requires name operand.");
            }
            return string.Compare(Name, n.Name, StringComparison.Ordinal);
        }
    }
}
