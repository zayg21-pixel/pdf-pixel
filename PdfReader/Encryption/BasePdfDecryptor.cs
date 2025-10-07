using PdfReader.Models;
using System;

namespace PdfReader.Encryption
{
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
        /// Decrypt raw bytes belonging to an indirect object (string or stream) identified by its reference.
        /// Implementations must return a new memory block (never mutate the input span in place).
        /// If encryption is not applicable the input bytes should be returned unchanged.
        /// </summary>
        /// <param name="data">Encrypted (or plain) bytes.</param>
        /// <param name="reference">Owning object reference.</param>
        public abstract ReadOnlyMemory<byte> DecryptBytes(ReadOnlyMemory<byte> data, PdfReference reference);

        protected string Password { get; private set; }

        public virtual void UpdatePassword(string password)
        {
            Password = password ?? string.Empty;
            // Derived classes may recompute file key when password changes.
        }
    }
}
