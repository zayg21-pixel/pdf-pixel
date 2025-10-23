using System.Collections.Generic;

namespace PdfReader.Rendering.Functions
{
    /// <summary>
    /// Represents a token in the PostScript expression stream.
    /// </summary>
    public abstract class PostScriptToken
    {
    }

    /// <summary>
    /// Represents a numeric token in PostScript.
    /// </summary>
    public class PostScriptNumber : PostScriptToken
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostScriptNumber"/> class.
        /// </summary>
        /// <param name="value">The numeric value.</param>
        public PostScriptNumber(float value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the numeric value.
        /// </summary>
        public float Value { get; }
    }

    /// <summary>
    /// Represents an operator token in PostScript.
    /// </summary>
    public class PostScriptOperator : PostScriptToken
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostScriptOperator"/> class.
        /// </summary>
        /// <param name="name">The operator name.</param>
        public PostScriptOperator(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the operator name.
        /// </summary>
        public string Name { get; }
    }

    /// <summary>
    /// Represents a procedure (block) token in PostScript.
    /// </summary>
    public class PostScriptProcedure : PostScriptToken
    {
        /// <summary>
        /// Gets the list of tokens in the procedure.
        /// </summary>
        public List<PostScriptToken> Tokens { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostScriptProcedure"/> class.
        /// </summary>
        /// <param name="tokens">The tokens in the procedure.</param>
        public PostScriptProcedure(List<PostScriptToken> tokens)
        {
            Tokens = tokens;
        }
    }
}
