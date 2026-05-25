using System;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif

namespace PdfPixel.Text
{
    /// <summary>
    /// Marks the default value for a PDF enum type. The field marked with this attribute must be equal to default(T).
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    internal class PdfEnumDefaultValueAttribute : Attribute;
}
