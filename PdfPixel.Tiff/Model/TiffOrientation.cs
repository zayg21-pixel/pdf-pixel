namespace PdfPixel.Tiff.Model;

/// <summary>
/// Placement of the stored rows and columns relative to the displayed image (tag 274).
/// </summary>
public enum TiffOrientation
{
    /// <summary>
    /// Orientation not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Row 0 is the top, column 0 the left.
    /// </summary>
    TopLeft = 1,

    /// <summary>
    /// Row 0 is the top, column 0 the right.
    /// </summary>
    TopRight = 2,

    /// <summary>
    /// Row 0 is the bottom, column 0 the right.
    /// </summary>
    BottomRight = 3,

    /// <summary>
    /// Row 0 is the bottom, column 0 the left.
    /// </summary>
    BottomLeft = 4,

    /// <summary>
    /// Row 0 is the left side, column 0 the top.
    /// </summary>
    LeftTop = 5,

    /// <summary>
    /// Row 0 is the right side, column 0 the top.
    /// </summary>
    RightTop = 6,

    /// <summary>
    /// Row 0 is the right side, column 0 the bottom.
    /// </summary>
    RightBottom = 7,

    /// <summary>
    /// Row 0 is the left side, column 0 the bottom.
    /// </summary>
    LeftBottom = 8
}
