namespace PdfReader.Models
{
    public enum PdfValueType
    {
        Name,
        String, 
        Operator, 
        Integer,
        Real,
        Reference,
        Array,
        Dictionary,
        HexString
    }
}