using System;

namespace PdfPixel.Objects;

/// <summary>
/// Marks an enum type as a PDF enum for use with PdfEnumUtilities.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
internal sealed class PdfEnumAttribute : Attribute;
