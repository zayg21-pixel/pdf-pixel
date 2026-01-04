using System;

namespace PdfReader.Annotations.Models;

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
    Hidden = 2,

    /// <summary>
    /// If set, print the annotation when the page is printed.
    /// </summary>
    Print = 4,

    /// <summary>
    /// If set, do not scale the annotation's appearance to match the magnification of the page.
    /// </summary>
    NoZoom = 8,

    /// <summary>
    /// If set, do not rotate the annotation's appearance to match the rotation of the page.
    /// </summary>
    NoRotate = 16,

    /// <summary>
    /// If set, do not display the annotation on the screen or allow it to interact with the user.
    /// </summary>
    NoView = 32,

    /// <summary>
    /// If set, do not allow the annotation to interact with the user.
    /// </summary>
    ReadOnly = 64,

    /// <summary>
    /// If set, do not allow the annotation to be deleted or its properties to be modified by the user.
    /// </summary>
    Locked = 128,

    /// <summary>
    /// If set, invert the interpretation of the NoView flag for certain events.
    /// </summary>
    ToggleNoView = 256,

    /// <summary>
    /// If set, do not allow the content of the annotation to be modified by the user.
    /// </summary>
    LockedContents = 512
}