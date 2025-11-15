namespace PdfReader.Models
{
    public enum PdfValueType
    {
        Null,
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