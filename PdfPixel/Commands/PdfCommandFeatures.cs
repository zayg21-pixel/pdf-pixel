using System;

namespace PdfPixel.Commands;

/// <summary>
/// Describes the decoding capabilities of a <see cref="IPdfCommand"/> that affect
/// when a cached picture must be regenerated.
/// </summary>
[Flags]
public enum PdfCommandFeatures
{
    /// <summary>
    /// Command output does not depend on scale or visible region.
    /// </summary>
    None = 0,

    /// <summary>
    /// Command decodes only the visible region of the page.
    /// Any change to the visible region requires regeneration.
    /// </summary>
    Region = 1 << 0,

    /// <summary>
    /// Command produces different output at different zoom levels.
    /// A scale change requires regeneration.
    /// </summary>
    Scale = 1 << 1,

    /// <summary>
    /// Command owns or references a resource (e.g. an image cache) that other commands appearing
    /// later in the same recording also read from. A processor that executes and disposes commands
    /// immediately, such as <see cref="SkCanvasCommandProcessor"/>, must not dispose this command
    /// until every command in the recording has run, otherwise later commands would read from an
    /// already-disposed resource.
    /// </summary>
    DeferredDispose = 1 << 2
}
