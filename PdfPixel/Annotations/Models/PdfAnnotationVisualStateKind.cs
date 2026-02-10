using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the visual interaction state for annotation rendering.
/// </summary>
[Flags]
public enum PdfAnnotationVisualStateKind
{
    /// <summary>
    /// No state specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Default rendering state.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Pointer is over the annotation (rollover).
    /// </summary>
    Rollover = 2,

    /// <summary>
    /// Pointer button is pressed on the annotation (down).
    /// </summary>
    Down = 4
}