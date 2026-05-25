namespace PdfPixel.PostScript.Tokens
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
}
