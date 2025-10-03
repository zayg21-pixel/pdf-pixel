using System;

namespace PdfReader.Models
{
    public class PdfObject
    {
        public PdfObject(PdfReference reference, PdfDocument document, IPdfValue value)
        {
            Reference = reference;
            Document = document;
            Value = value;
            Dictionary = value.AsDictionary() ?? new PdfDictionary(document);
        }

        public PdfReference Reference { get; }

        public PdfDocument Document { get; }

        public IPdfValue Value { get; }

        public PdfDictionary Dictionary { get; }

        public ReadOnlyMemory<byte> StreamData { get; set; }
    }
}