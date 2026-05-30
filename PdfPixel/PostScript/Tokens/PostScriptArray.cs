using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// PostScript array object (literal [ ... ] or created via 'array').
    /// </summary>
    public sealed class PostScriptArray : PostScriptToken, IPostScriptCollection
    {
        /// <summary>
        /// Initializes an array with the specified capacity. All elements are initially null.
        /// </summary>
        /// <param name="capacity">The fixed length of the array.</param>
        public PostScriptArray(int capacity) => Elements = new PostScriptToken[capacity];

        /// <summary>
        /// Initializes an array wrapping the supplied element sequence directly.
        /// </summary>
        /// <param name="elements">The token elements to store in this array.</param>
        public PostScriptArray(PostScriptToken[] elements) => Elements = elements;

        /// <summary>
        /// Gets the fixed-length array of token elements held by this PostScript array object.
        /// </summary>
        public PostScriptToken[] Elements { get; }

        /// <summary>
        /// Gets the elements as a read-only list, satisfying <see cref="IPostScriptCollection"/>.
        /// </summary>
        public IReadOnlyList<PostScriptToken> Items => Elements;

        /// <summary>
        /// Returns a diagnostic string showing the number of elements in the array.
        /// </summary>
        public override string ToString()
        {
            int count = (Elements?.Length) ?? 0;
            return "Array(count=" + count + ")";
        }

        /// <summary>
        /// Returns true only when <paramref name="other"/> is the same object instance, matching PostScript array identity semantics.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

        /// <summary>
        /// Returns a hash code based on object identity.
        /// </summary>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

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

            var index = (int)number.Number;
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

            var index = (int)number.Number;
            if (index < 0 || index >= Elements.Length)
            {
                throw new InvalidOperationException("rangecheck: array index out of range");
            }

            Elements[index] = value;
        }
    }
}
