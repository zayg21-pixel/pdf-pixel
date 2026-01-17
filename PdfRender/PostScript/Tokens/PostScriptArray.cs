using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;

namespace PdfRender.PostScript.Tokens
{
    /// <summary>
    /// PostScript array object (literal [ ... ] or created via 'array').
    /// </summary>
    public sealed class PostScriptArray : PostScriptToken, IPostScriptCollection
    {
        public PostScriptArray(int capacity)
        {
            Elements = new PostScriptToken[capacity];
        }
        public PostScriptArray(PostScriptToken[] elements)
        {
            Elements = elements;
        }

        public PostScriptToken[] Elements { get; }

        public IReadOnlyList<PostScriptToken> Items => Elements;

        public override string ToString()
        {
            int count = Elements == null ? 0 : Elements.Length;
            return "Array(count=" + count + ")";
        }
        public override bool EqualsToken(PostScriptToken other)
        {
            return ReferenceEquals(this, other);
        }
        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        /// <summary>
        /// Retrieve element at numeric index.
        /// </summary>
        public override PostScriptToken GetValue(PostScriptToken keyOrIndex)
        {
            EnsureAccess(PostScriptAccessOperation.Read);
            if (keyOrIndex is not PostScriptNumber number)
            {
                throw new InvalidOperationException("typecheck: array index must be number");
            }
            int index = (int)number.Value;
            if (index < 0 || index >= Elements.Length)
            {
                throw new InvalidOperationException("rangecheck: array index out of range");
            }
            return Elements[index];
        }

        /// <summary>
        /// Set element at numeric index.
        /// </summary>
        public override void SetValue(PostScriptToken keyOrIndex, PostScriptToken value)
        {
            EnsureAccess(PostScriptAccessOperation.Modify);
            if (keyOrIndex is not PostScriptNumber number)
            {
                throw new InvalidOperationException("typecheck: array index must be number");
            }
            int index = (int)number.Value;
            if (index < 0 || index >= Elements.Length)
            {
                throw new InvalidOperationException("rangecheck: array index out of range");
            }
            Elements[index] = value;
        }
    }
}
