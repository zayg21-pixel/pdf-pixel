using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the various PDF annotation flags as defined in the PDF specification.
/// </summary>
[Flags]
public enum PdfAnnotationFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// If set, do not display the annotation if it does not belong to one of the standard annotation types.
    /// </summary>
    Invisible = 1,

    /// <summary>
    /// If set, do not display or print the annotation or allow it to interact with the user.
    /// </summary>
    Hidden = 1 << 1,

    /// <summary>
    /// If set, print the annotation when the page is printed.
    /// </summary>
    Print = 1 << 2,

    // TODO: [MEDIUM] NoZoom is not honored by rendering. Annotations should keep a fixed device-pixel
    // size across page zoom levels; today annotation commands are recorded in PDF user-space units and
    // scaled with the rest of the page.
    /// <summary>
    /// If set, do not scale the annotation's appearance to match the magnification of the page.
    /// </summary>
    NoZoom = 1 << 3,

    /// <summary>
    /// If set, do not rotate the annotation's appearance to match the rotation of the page.
    /// </summary>
    NoRotate = 1 << 4,

    /// <summary>
    /// If set, do not display the annotation on the screen or allow it to interact with the user.
    /// </summary>
    NoView = 1 << 5,

    /// <summary>
    /// If set, do not allow the annotation to interact with the user.
    /// </summary>
    ReadOnly = 1 << 6,

    /// <summary>
    /// If set, do not allow the annotation to be deleted or its properties to be modified by the user.
    /// </summary>
    Locked = 1 << 7,

    /// <summary>
    /// If set, invert the interpretation of the NoView flag for certain events.
    /// </summary>
    ToggleNoView = 1 << 8,

    /// <summary>
    /// If set, do not allow the content of the annotation to be modified by the user.
    /// </summary>
    LockedContents = 1 << 9
}
