namespace PdfReader.Models
{
    public enum PdfValueType
    {
        Name,
        String, 
        Boolean,
        Operator, 
        Integer,
        Real,
        Reference,
        Array,
        Dictionary,
        InlineStream
    }
}