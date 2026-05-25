using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and metadata.
/// </summary>
public interface IPdfDocument : IDisposable
{
    /// <summary>
    /// Gets the list of pages in the PDF document.
    /// </summary>
    IReadOnlyList<IPdfPage> Pages { get; }

}
