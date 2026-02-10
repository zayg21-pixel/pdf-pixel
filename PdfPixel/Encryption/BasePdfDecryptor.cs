using PdfPixel.Models;
using System;
using System.IO;

namespace PdfPixel.Encryption
{
    // TODO: [MEDIUM] implement missing decryptions
    /// <summary>
    /// Base decryptor that exposes unified byte decryption for both streams and string objects.
    /// Implementations derive file and object specific keys internally.
    /// </summary>
    public abstract class BasePdfDecryptor
    {
        protected BasePdfDecryptor(PdfDecryptorParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public PdfDecryptorParameters Parameters { get; }

        /// <summary>
        /// Decrypt raw bytes belonging to an indirect string object identified by its reference.
        /// Implementations must return a new memory block (never mutate the input span in place).
        /// If encryption is not applicable the input bytes should be returned unchanged.
        /// </summary>
        /// <param name="data">Encrypted (or plain) bytes.</param>
        /// <param name="reference">Owning object reference.</param>
        public abstract ReadOnlyMemory<byte> DecryptString(ReadOnlyMemory<byte> data, PdfReference reference);

        /// <summary>
        /// Decrypts the contents of the specified stream using the provided PDF reference.
        /// </summary>
        /// <remarks>The method reads the entire content of the input stream, decrypts it, and returns a
        /// new memory stream containing the decrypted data. The input stream is not modified or disposed by this
        /// method.</remarks>
        /// <param name="stream">The input stream containing the encrypted data. The stream must be readable.</param>
        /// <param name="reference">The PDF reference used to determine the decryption parameters.</param>
        /// <returns>A stream containing the decrypted data. The caller is responsible for disposing of the returned stream.</returns>
        public virtual Stream DecryptStream(Stream stream, PdfReference reference)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var decryptedBytes = DecryptString(memoryStream.ToArray(), reference);
            return new MemoryStream(decryptedBytes.ToArray());
        } // TODO: [MEDIUM] need to optimize to avoid double memory copy

        protected string Password { get; private set; }

        public virtual void UpdatePassword(string password)
        {
            Password = password ?? string.Empty;
            // Derived classes may recompute file key when password changes.
        }
    }
}
