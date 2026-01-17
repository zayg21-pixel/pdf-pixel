using System;

namespace PdfRender.PostScript.Tokens
{
    /// <summary>
    /// Access qualifiers applied to composite PostScript objects (arrays, dictionaries, procedures, strings) by operators
    /// such as readonly, executeonly, and noaccess. The interpreter enforces mutation restrictions for all non-Normal levels.
    /// For now, ExecuteOnly and ReadOnly behave the same (prevent mutation); NoAccess prevents both mutation and value retrieval.
    /// </summary>
    public enum PostScriptAccess
    {
        Normal = 0,
        ReadOnly = 1,
        ExecuteOnly = 2,
        NoAccess = 3
    }

    /// <summary>
    /// Describes the kind of access an operation intends to perform on a PostScript object.
    /// Read: inspect or retrieve contents (e.g. get, enumeration).
    /// Modify: mutate contents (e.g. put, def, changing array/dict elements).
    /// Execute: execute a procedure body (token sequence evaluation).
    /// </summary>
    public enum PostScriptAccessOperation
    {
        Read,
        Modify,
        Execute
    }

    /// <summary>
    /// Base class for PostScript tokens.
    /// Provides equality and relational operator support for applicable derived types.
    /// </summary>
    public abstract class PostScriptToken : IEquatable<PostScriptToken>
    {
        /// <summary>
        /// Access level for this token.
        /// </summary>
        public PostScriptAccess AccessLevel { get; private set; }

        /// <summary>
        /// Performs a comparison (like IComparable) returning negative for less, zero for equal, positive for greater.
        /// Implementations must throw InvalidOperationException if comparison with the supplied token type is not supported.
        /// </summary>
        /// <param name="other">Other token.</param>
        /// <returns>Comparison result.</returns>
        public virtual int CompareToToken(PostScriptToken other)
        {
            throw new InvalidOperationException("Relational comparison not supported for these token types.");
        }

        /// <summary>
        /// Logical AND (boolean) or bitwise AND (integral numeric). Override in supported types.
        /// </summary>
        public virtual PostScriptToken LogicalAnd(PostScriptToken other)
        {
            throw new InvalidOperationException("AND not supported for these token types.");
        }

        /// <summary>
        /// Logical OR (boolean) or bitwise OR (integral numeric). Override in supported types.
        /// </summary>
        public virtual PostScriptToken LogicalOr(PostScriptToken other)
        {
            throw new InvalidOperationException("OR not supported for these token types.");
        }

        public virtual PostScriptToken LogicalXor(PostScriptToken other)
        {
            throw new InvalidOperationException("XOR not supported for these token types.");
        }

        /// <summary>
        /// Logical NOT (boolean) or bitwise complement (integral numeric). Override in supported types.
        /// </summary>
        public virtual PostScriptToken LogicalNot()
        {
            throw new InvalidOperationException("NOT not supported for this token type.");
        }

        /// <summary>
        /// Updates the access level for this token.
        /// </summary>
        /// <param name="access">New access level.</param>
        public void SetAccessLevel(PostScriptAccess access)
        {
            AccessLevel = access;
        }

        /// <summary>
        /// Ensures the requested access operation is permitted for this token's current <see cref="AccessLevel"/>.
        /// Throws <see cref="InvalidOperationException"/> if the operation is not allowed.
        /// Normal: all operations permitted.
        /// ReadOnly: Read permitted; Modify prohibited; Execute permitted only for procedures (caller decides applicability).
        /// ExecuteOnly: Execute permitted (for procedures); Read and Modify prohibited.
        /// NoAccess: All operations prohibited.
        /// </summary>
        /// <param name="operationType">Requested access operation.</param>
        public void EnsureAccess(PostScriptAccessOperation operationType)
        {
            switch (AccessLevel)
            {
                case PostScriptAccess.Normal:
                {
                    return;
                }
                case PostScriptAccess.ReadOnly:
                {
                    if (operationType == PostScriptAccessOperation.Modify)
                    {
                        throw new InvalidOperationException("Modify operation not permitted on readonly PostScript object.");
                    }
                    return;
                }
                case PostScriptAccess.ExecuteOnly:
                {
                    if (operationType != PostScriptAccessOperation.Execute)
                    {
                        throw new InvalidOperationException("Access denied: execute-only PostScript object (read/modify not permitted).");
                    }
                    return;
                }
                case PostScriptAccess.NoAccess:
                {
                    throw new InvalidOperationException("Access denied: noaccess PostScript object.");
                }
                default:
                {
                    throw new InvalidOperationException("Unknown access level.");
                }
            }
        }

        /// <summary>
        /// Retrieve a value from a composite PostScript object (array, string, dictionary) given an index or key token.
        /// Base implementation throws; derived composite types override.
        /// </summary>
        /// <param name="keyOrIndex">Index (numeric) for arrays/strings or literal name for dictionaries.</param>
        /// <returns>The retrieved element token.</returns>
        /// <exception cref="InvalidOperationException">Always thrown in base class.</exception>
        public virtual PostScriptToken GetValue(PostScriptToken keyOrIndex)
        {
            throw new InvalidOperationException("getvalue: operation not supported on this token type");
        }

        /// <summary>
        /// Set a value within a composite PostScript object. Base implementation throws; overrides will validate
        /// access rights, key/index validity, and perform assignment. Provided as a hook for implementing universal put logic.
        /// </summary>
        /// <param name="keyOrIndex">Index (numeric) or literal name key.</param>
        /// <param name="value">Value token to assign.</param>
        /// <exception cref="InvalidOperationException">Always thrown in base class.</exception>
        public virtual void SetValue(PostScriptToken keyOrIndex, PostScriptToken value)
        {
            throw new InvalidOperationException("setvalue: operation not supported on this token type");
        }

        public abstract bool EqualsToken(PostScriptToken other);

        public bool Equals(PostScriptToken other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null)
            {
                return false;
            }
            return EqualsToken(other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PostScriptToken);
        }

        public static bool operator ==(PostScriptToken left, PostScriptToken right)
        {
            if (left is null && right is null)
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.EqualsToken(right);
        }

        public static bool operator !=(PostScriptToken left, PostScriptToken right)
        {
            return !(left == right);
        }

        public static bool operator >(PostScriptToken left, PostScriptToken right)
        {
            return left?.CompareToToken(right) > 0;
        }
        public static bool operator <(PostScriptToken left, PostScriptToken right)
        {
            return left?.CompareToToken(right) < 0;
        }
        public static bool operator >=(PostScriptToken left, PostScriptToken right)
        {
            return left?.CompareToToken(right) >= 0;
        }
        public static bool operator <=(PostScriptToken left, PostScriptToken right)
        {
            return left?.CompareToToken(right) <= 0;
        }

        public static PostScriptToken operator &(PostScriptToken left, PostScriptToken right)
        {
            return left?.LogicalAnd(right);
        }
        public static PostScriptToken operator |(PostScriptToken left, PostScriptToken right)
        {
            return left?.LogicalOr(right);
        }

        public static PostScriptToken operator ^(PostScriptToken left, PostScriptToken right)
        {
            return left?.LogicalXor(right);
        }

        public static PostScriptToken operator !(PostScriptToken operand)
        {
            return operand?.LogicalNot();
        }

        public abstract override int GetHashCode();

        /// <summary>
        /// Returns a diagnostic string representation of the token.
        /// Derived types override this for specific formatting.
        /// </summary>
        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
