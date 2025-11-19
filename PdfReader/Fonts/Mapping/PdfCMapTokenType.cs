using PdfReader.Text;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Enumerates tokens encountered in a ToUnicode / CMap stream used for CID font character mapping.
/// Values are mapped from raw operator strings via the PdfEnum infrastructure for fast enum conversion.
/// </summary>
[PdfEnum]
public enum PdfCMapTokenType
{
    /// <summary>
    /// Unknown or unmapped token. Default fallback value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,
    /// <summary>
    /// Begins a bfchar block (individual code to Unicode mappings).
    /// </summary>
    [PdfEnumValue("beginbfchar")]
    BeginBfChar,
    /// <summary>
    /// Ends a bfchar block.
    /// </summary>
    [PdfEnumValue("endbfchar")]
    EndBfChar,
    /// <summary>
    /// Begins a bfrange block (code range to sequential Unicode mappings or array form).
    /// </summary>
    [PdfEnumValue("beginbfrange")]
    BeginBfRange,
    /// <summary>
    /// Ends a bfrange block.
    /// </summary>
    [PdfEnumValue("endbfrange")]
    EndBfRange,
    /// <summary>
    /// Begins a cidchar block (code bytes to CID mappings).
    /// </summary>
    [PdfEnumValue("begincidchar")]
    BeginCidChar,
    /// <summary>
    /// Ends a cidchar block.
    /// </summary>
    [PdfEnumValue("endcidchar")]
    EndCidChar,
    /// <summary>
    /// Begins a cidrange block (code range to sequential CID mappings).
    /// </summary>
    [PdfEnumValue("begincidrange")]
    BeginCidRange,
    /// <summary>
    /// Ends a cidrange block.
    /// </summary>
    [PdfEnumValue("endcidrange")]
    EndCidRange,
    /// <summary>
    /// Begins a codespacerange block (defines valid code byte lengths / ranges).
    /// </summary>
    [PdfEnumValue("begincodespacerange")]
    BeginCodespaceRange,
    /// <summary>
    /// Ends a codespacerange block.
    /// </summary>
    [PdfEnumValue("endcodespacerange")]
    EndCodespaceRange,
    /// <summary>
    /// References a base CMap to merge (usecmap operator).
    /// </summary>
    [PdfEnumValue("usecmap")]
    UseCMap
}
