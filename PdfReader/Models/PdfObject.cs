using CommunityToolkit.HighPerformance;
using PdfReader.Streams;
using PdfReader.Text;
using System;
using System.IO;

namespace PdfReader.Models
{
    public struct PdfObjectStreamReference
    {
        public PdfObjectStreamReference(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }

        public int Length { get; }
    }

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

        public PdfObjectStreamReference? StreamInfo { get; internal set; }

        public bool HasStream => StreamInfo.HasValue;

        /// <summary>
        /// Embedded stream data, if available.
        /// </summary>
        internal ReadOnlyMemory<byte> EmbaddedStream { get; set; }

        public Stream GetRawStream()
        {
            if (!EmbaddedStream.IsEmpty)
            {
                return EmbaddedStream.AsStream();
            }

            if (!HasStream)
            {
                return Stream.Null;
            }

            var subrange = new SubrangeReadOnlyStream(Document.Stream, StreamInfo.Value.Offset, StreamInfo.Value.Length, leaveOpen: true);

            if (Document.Decryptor != null && Reference.IsValid)
            {
                return Document.Decryptor.DecryptStream(subrange, Reference);
            }
            else
            {
                return subrange;
            }
        }

        public Stream DecodeAsStream() // TODO: use
        {
            if (!HasStream)
            {
                return Stream.Null;
            }

            return Document.StreamDecoder.DecodeContentAsStream(this);
        }

        public ReadOnlyMemory<byte> DecodeAsMemory()
        {
            if (!HasStream)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return Document.StreamDecoder.DecodeContentStream(this);
        }
    }
}