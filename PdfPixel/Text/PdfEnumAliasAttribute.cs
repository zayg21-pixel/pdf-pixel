using System;

namespace PdfPixel.Text
{
    /// <summary>
    /// Specifies an alternative PDF string value that maps to the same enum field.
    /// Aliases participate only in forward lookup (string to enum), not in reverse (enum to string).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal sealed class PdfEnumAliasAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfEnumAliasAttribute"/> class with the specified alias name.
        /// </summary>
        /// <param name="name">The alternative PDF string name for the enum value.</param>
        public PdfEnumAliasAttribute(string name) => Name = name;

        /// <summary>
        /// Gets the alias PDF string name.
        /// </summary>
        public string Name { get; }
    }
}
