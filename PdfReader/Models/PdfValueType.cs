namespace PdfReader.Models;

/// <summary>
/// Specifies the type of value represented in a PDF document.
/// </summary>
public enum PdfValueType
{
    /// <summary>
    /// Represents a PDF null object.
    /// </summary>
    Null,
    /// <summary>
    /// Represents a PDF name object (identifier starting with '/').
    /// </summary>
    Name,
    /// <summary>
    /// Represents a PDF string object (literal or hexadecimal).
    /// </summary>
    String, 
    /// <summary>
    /// Represents a PDF boolean value (true or false).
    /// </summary>
    Boolean,
    /// <summary>
    /// Represents a PDF operator (used in content streams).
    /// </summary>
    Operator,
    /// <summary>
    /// Represents a PDF integer number.
    /// </summary>
    Integer,
    /// <summary>
    /// Represents a PDF real (floating-point) number.
    /// </summary>
    Real,
    /// <summary>
    /// Represents a PDF indirect reference to another object.
    /// </summary>
    Reference,
    /// <summary>
    /// Represents a PDF array object.
    /// </summary>
    Array,
    /// <summary>
    /// Represents a PDF dictionary object (key-value pairs).
    /// </summary>
    Dictionary,
    /// <summary>
    /// Represents a PDF inline stream (embedded data within content streams).
    /// </summary>
    InlineStream
}