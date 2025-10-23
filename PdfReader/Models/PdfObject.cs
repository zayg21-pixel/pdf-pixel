using System;
using System.IO;

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

        public Stream DecodeAsStream() // TOOD: use those overloads instead!
        {
            return Document.StreamDecoder.DecodeContentAsStream(this);
        }

        public ReadOnlyMemory<byte> DecodeAsMemory()
        {
            return Document.StreamDecoder.DecodeContentStream(this);
        }
    }
}