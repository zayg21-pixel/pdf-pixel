namespace PdfPixel.Models;

/// <summary>
/// Specifies the type of value represented in a PDF document.
/// </summary>
public enum PdfValueType : byte
{
    /// <summary>
    /// Represents a PDF null object.
    /// </summary>
    Null = 0,

    /// <summary>
    /// Represents a PDF name object (identifier starting with '/').
    /// </summary>
    Name = 1,

    /// <summary>
    /// Represents a PDF string object (literal or hexadecimal).
    /// </summary>
    String = 2,

    /// <summary>
    /// Represents a PDF boolean value (true or false).
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// Represents a PDF operator (used in content streams).
    /// </summary>
    Operator = 4,

    /// <summary>
    /// Represents a PDF integer number.
    /// </summary>
    Integer = 5,

    /// <summary>
    /// Represents a PDF real (floating-point) number.
    /// </summary>
    Real = 6,

    /// <summary>
    /// Represents a PDF indirect reference to another object.
    /// </summary>
    Reference = 7,

    /// <summary>
    /// Represents a PDF array object.
    /// </summary>
    Array = 8,

    /// <summary>
    /// Represents a PDF dictionary object (key-value pairs).
    /// </summary>
    Dictionary = 9,

    /// <summary>
    /// Represents a fully parsed inline image (BI/ID/EI) embedded within a content stream,
    /// carrying its image dictionary and raw stream bytes as a <see cref="PdfObject"/>.
    /// </summary>
    InlineImage = 10
}
