using PdfReader.Text;

namespace PdfReader.Models
{
    /// <summary>
    /// Enumerates the supported XObject subtypes for PDF resources.
    /// </summary>
    [PdfEnum]
    public enum PdfXObjectSubtype
    {
        [PdfEnumDefaultValue]
        Unknown,

        /// <summary>
        /// Form XObject subtype (/Form)
        /// </summary>
        [PdfEnumValue("Form")]
        Form,

        /// <summary>
        /// Image XObject subtype (/Image)
        /// </summary>
        [PdfEnumValue("Image")]
        Image,

        /// <summary>
        /// PostScript XObject subtype (/PS)
        /// </summary>
        [PdfEnumValue("PS")]
        PS,

        /// <summary>
        /// XML XObject subtype (/XML, PDF 1.6+)
        /// </summary>
        [PdfEnumValue("XML")]
        XML,

        /// <summary>
        /// Movie XObject subtype (/Movie, PDF 1.2+)
        /// </summary>
        [PdfEnumValue("Movie")]
        Movie,

        /// <summary>
        /// Sound XObject subtype (/Sound, PDF 1.2+)
        /// </summary>
        [PdfEnumValue("Sound")]
        Sound
    }

    /// <summary>
    /// Represents a PDF XObject.
    /// </summary>
    public class PdfXObject
    {
        public PdfXObject(PdfObject xObject, PdfXObjectSubtype subtype)
        {
            XObject = xObject;
            Subtype = subtype;
        }

        /// <summary>
        /// Source PDF object representing the XObject.
        /// </summary>
        public PdfObject XObject { get; }

        /// <summary>
        /// Subtype of the XObject.
        /// </summary>
        public PdfXObjectSubtype Subtype { get; }

        public static PdfXObject FromObject(PdfObject sourceObject)
        {
            var subtype = sourceObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfXObjectSubtype>();
            return new PdfXObject(sourceObject, subtype);
        }
    }
}
