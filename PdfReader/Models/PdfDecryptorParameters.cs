using System.Collections.Generic;

namespace PdfReader.Models
{
    /// <summary>
    /// Aggregates values from the /Encrypt dictionary and the trailer /ID array required to initialize a decryptor.
    /// Holds only extracted scalar values; decryptor implementations can still access the raw dictionary via SourceDictionary
    /// to parse advanced or version-specific fields.
    /// </summary>
    public class PdfDecryptorParameters
    {
        /// <summary>
        /// Original /Encrypt dictionary for advanced lookups (e.g. /CF, /O, /U, /UE, /OE, /Perms).
        /// </summary>
        public PdfDictionary SourceDictionary { get; set; }

        /// <summary>
        /// Encryption algorithm version (/V).
        /// </summary>
        public int V { get; set; }

        /// <summary>
        /// Security handler revision (/R).
        /// </summary>
        public int R { get; set; }

        /// <summary>
        /// Key length in bits (/Length). May be 0 if absent (defaults apply later).
        /// </summary>
        public int LengthBits { get; set; }

        /// <summary>
        /// Permissions (/P) raw integer.
        /// </summary>
        public int Permissions { get; set; }

        /// <summary>
        /// Indicates if metadata is encrypted (/EncryptMetadata, default true when absent).
        /// </summary>
        public bool EncryptMetadata { get; set; } = true;

        /// <summary>
        /// First file identifier (/ID[0]).
        /// </summary>
        public byte[] FileIdFirst { get; set; }

        /// <summary>
        /// Second file identifier (/ID[1]).
        /// </summary>
        public byte[] FileIdSecond { get; set; }

        /// <summary>
        /// Owner password entry (/O) raw bytes (32 for R<=4, 48 for R>=5).
        /// </summary>
        public byte[] OwnerEntry { get; set; }

        /// <summary>
        /// User password entry (/U) raw bytes (32 for R<=4, 48 for R>=5).
        /// </summary>
        public byte[] UserEntry { get; set; }

        /// <summary>
        /// Encrypted file key using owner password (/OE) - R>=5.
        /// </summary>
        public byte[] OwnerEncryptedKey { get; set; }

        /// <summary>
        /// Encrypted file key using user password (/UE) - R>=5.
        /// </summary>
        public byte[] UserEncryptedKey { get; set; }

        /// <summary>
        /// Permissions block (/Perms) - R>=5.
        /// </summary>
        public byte[] Perms { get; set; }

        /// <summary>
        /// Stream crypt filter name (/StmF) when V=4 or 5.
        /// </summary>
        public PdfString StreamCryptFilterName { get; set; }

        /// <summary>
        /// String crypt filter name (/StrF) when V=4 or 5.
        /// </summary>
        public PdfString StringCryptFilterName { get; set; }

        /// <summary>
        /// Embedded file crypt filter name (/EFF) when present.
        /// </summary>
        public PdfString EmbeddedFileCryptFilterName { get; set; }

        /// <summary>
        /// Crypt filter dictionary (/CF) parsed lazily by decryptor. Raw map retained from SourceDictionary when needed.
        /// </summary>
        public PdfDictionary CryptFilterDictionary { get; set; }
    }
}
