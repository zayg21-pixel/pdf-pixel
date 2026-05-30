namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Describes the kind of access an operation intends to perform on a PostScript object.
    /// Read: inspect or retrieve contents (e.g. get, enumeration).
    /// Modify: mutate contents (e.g. put, def, changing array/dict elements).
    /// Execute: execute a procedure body (token sequence evaluation).
    /// </summary>
    public enum PostScriptAccessOperation
    {
        /// <summary>
        /// Inspect or retrieve the contents of an object, such as get or enumeration.
        /// </summary>
        Read,

        /// <summary>
        /// Mutate the contents of an object, such as put or def.
        /// </summary>
        Modify,

        /// <summary>
        /// Execute a procedure body (token sequence evaluation).
        /// </summary>
        Execute
    }
}
