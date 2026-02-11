using System;

namespace PdfPixel.Forms;

/// <summary>
/// Flags for PDF form fields.
/// </summary>
[Flags]
public enum PdfFormFieldFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Field is read-only.
    /// </summary>
    ReadOnly = 1 << 0,

    /// <summary>
    /// Field is required.
    /// </summary>
    Required = 1 << 1,

    /// <summary>
    /// Field value should not be exported.
    /// </summary>
    NoExport = 1 << 2,

    /// <summary>
    /// Text field - multiline.
    /// </summary>
    Multiline = 1 << 12,

    /// <summary>
    /// Text field - password.
    /// </summary>
    Password = 1 << 13,

    /// <summary>
    /// Text field - file select.
    /// </summary>
    FileSelect = 1 << 20,

    /// <summary>
    /// Text field - do not spell check.
    /// </summary>
    DoNotSpellCheck = 1 << 22,

    /// <summary>
    /// Text field - do not scroll.
    /// </summary>
    DoNotScroll = 1 << 23,

    /// <summary>
    /// Text field - comb formatting.
    /// </summary>
    Comb = 1 << 24,

    /// <summary>
    /// Text field - rich text.
    /// </summary>
    RichText = 1 << 25,

    /// <summary>
    /// Button field - no toggle to off.
    /// </summary>
    NoToggleToOff = 1 << 14,

    /// <summary>
    /// Button field - radio buttons in group are mutually exclusive.
    /// </summary>
    Radio = 1 << 15,

    /// <summary>
    /// Button field - push button.
    /// </summary>
    PushButton = 1 << 16,

    /// <summary>
    /// Button field - radios same value export same value.
    /// </summary>
    RadiosInUnison = 1 << 25,

    /// <summary>
    /// Choice field - combo box.
    /// </summary>
    Combo = 1 << 17,

    /// <summary>
    /// Choice field - editable combo box.
    /// </summary>
    Edit = 1 << 18,

    /// <summary>
    /// Choice field - sorted options.
    /// </summary>
    Sort = 1 << 19,

    /// <summary>
    /// Choice field - allow multiple selection.
    /// </summary>
    MultiSelect = 1 << 21,

    /// <summary>
    /// Choice field - commit on selection change.
    /// </summary>
    CommitOnSelChange = 1 << 26
}
