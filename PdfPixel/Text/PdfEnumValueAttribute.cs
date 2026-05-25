using System;

namespace PdfPixel.Text
{

    /// <summary>
    /// Specifies the PDF string value for an enum field.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    internal class PdfEnumValueAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfEnumValueAttribute"/> class with the specified PDF name.
        /// </summary>
        /// <param name="name">The PDF string name for the enum value.</param>
        public PdfEnumValueAttribute(string name) => Name = name;

        /// <summary>
        /// Gets the PDF string name associated with the enum value.
        /// </summary>
        public string Name { get; }
    }
}
