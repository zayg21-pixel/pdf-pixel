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
        Read,
        Modify,
        Execute
    }
}
