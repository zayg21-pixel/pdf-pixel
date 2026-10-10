using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Inline and block progression directions of a structure element (WritingMode).
/// </summary>
[PdfEnum]
public enum PdfStructureWritingMode
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Inline left to right, block top to bottom.
    /// </summary>
    [PdfEnumValue("LrTb")]
    LeftToRightTopToBottom,

    /// <summary>
    /// Inline right to left, block top to bottom.
    /// </summary>
    [PdfEnumValue("RlTb")]
    RightToLeftTopToBottom,

    /// <summary>
    /// Inline top to bottom, block right to left.
    /// </summary>
    [PdfEnumValue("TbRl")]
    TopToBottomRightToLeft,

    /// <summary>
    /// Inline top to bottom, block left to right (PDF 2.0).
    /// </summary>
    [PdfEnumValue("TbLr")]
    TopToBottomLeftToRight,

    /// <summary>
    /// Inline left to right, block bottom to top (PDF 2.0).
    /// </summary>
    [PdfEnumValue("LrBt")]
    LeftToRightBottomToTop,

    /// <summary>
    /// Inline right to left, block bottom to top (PDF 2.0).
    /// </summary>
    [PdfEnumValue("RlBt")]
    RightToLeftBottomToTop,

    /// <summary>
    /// Inline bottom to top, block right to left (PDF 2.0).
    /// </summary>
    [PdfEnumValue("BtRl")]
    BottomToTopRightToLeft,

    /// <summary>
    /// Inline bottom to top, block left to right (PDF 2.0).
    /// </summary>
    [PdfEnumValue("BtLr")]
    BottomToTopLeftToRight
}
