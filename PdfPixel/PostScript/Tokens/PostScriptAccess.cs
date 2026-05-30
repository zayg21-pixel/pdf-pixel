namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Access qualifiers applied to composite PostScript objects (arrays, dictionaries, procedures, strings) by operators
    /// such as readonly, executeonly, and noaccess. The interpreter enforces mutation restrictions for all non-Normal levels.
    /// For now, ExecuteOnly and ReadOnly behave the same (prevent mutation); NoAccess prevents both mutation and value retrieval.
    /// </summary>
    public enum PostScriptAccess
    {
        /// <summary>
        /// No restrictions; all read, modify, and execute operations are permitted.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Object contents may be read and, for procedures, executed, but not modified.
        /// </summary>
        /// 
        ReadOnly = 1,

        /// <summary>
        /// Only execution is permitted; reading and modifying the object are prohibited.
        /// </summary>
        ExecuteOnly = 2,

        /// <summary>
        /// All access is prohibited; read, modify, and execute operations all throw.
        /// </summary>
        NoAccess = 3
    }
}
