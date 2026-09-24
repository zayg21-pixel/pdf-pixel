using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Encryption;

/// <summary>
/// Base decryptor that exposes unified byte decryption for both streams and string objects.
/// Implementations derive file and object specific keys internally.
/// </summary>
public abstract class BasePdfDecryptor
{
    private readonly PdfPasswordRequestedCallback? _onPasswordRequested;
    private readonly object _authenticationLock = new();
    private volatile bool _isAuthenticated;

    /// <summary>
    /// Initializes the decryptor with the encryption parameters parsed from the PDF /Encrypt dictionary.
    /// </summary>
    /// <param name="parameters">Encryption parameters of the document.</param>
    /// <param name="onPasswordRequested">Called when the empty user password does not authenticate, or null to try only the empty password.</param>
    protected BasePdfDecryptor(PdfDecryptorParameters parameters, PdfPasswordRequestedCallback? onPasswordRequested)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _onPasswordRequested = onPasswordRequested;
    }

    /// <summary>
    /// Encryption parameters parsed from the document's /Encrypt dictionary.
    /// </summary>
    public PdfDecryptorParameters Parameters { get; }

    /// <summary>
    /// Decrypt raw bytes belonging to an indirect string object identified by its reference.
    /// </summary>
    /// <param name="data">Encrypted (or plain) bytes.</param>
    /// <param name="reference">Owning object reference.</param>
    /// <exception cref="PdfIncorrectPasswordException">Thrown if the string filter requires a password and none of the supplied passwords is correct.</exception>
    public byte[] DecryptString(in ReadOnlyMemory<byte> data, in PdfReference reference)
    {
        PdfCryptFilter cryptFilter = Parameters.StringCryptFilter;
        if (data.IsEmpty || cryptFilter.Method == PdfCryptFilterMethod.None)
        {
            return data.ToArray();
        }

        Authenticate(cryptFilter.AuthEvent);
        return Decrypt(data, reference, cryptFilter);
    }

    /// <summary>
    /// Decrypts the contents of the specified stream using the provided PDF reference.
    /// </summary>
    /// <remarks>The method reads the entire content of the input stream, decrypts it, and returns a
    /// new memory stream containing the decrypted data. The input stream is not modified or disposed by this
    /// method.</remarks>
    /// <param name="stream">The input stream containing the encrypted data. The stream must be readable.</param>
    /// <param name="reference">The PDF reference used to determine the decryption parameters.</param>
    /// <param name="cryptFilter">Crypt filter selected for the stream.</param>
    /// <returns>A stream containing the decrypted data. The caller is responsible for disposing of the returned stream.</returns>
    /// <exception cref="PdfIncorrectPasswordException">Thrown if the crypt filter requires a password and none of the supplied passwords is correct.</exception>
    public Stream DecryptStream(Stream stream, in PdfReference reference, PdfCryptFilter cryptFilter)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (cryptFilter == null)
        {
            throw new ArgumentNullException(nameof(cryptFilter));
        }

        if (cryptFilter.Method == PdfCryptFilterMethod.None)
        {
            return stream;
        }

        Authenticate(cryptFilter.AuthEvent);

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        byte[] decryptedBytes = (memoryStream.Length == 0)
            ? Array.Empty<byte>()
            : Decrypt(memoryStream.ToArray(), reference, cryptFilter);

        return new MemoryStream(decryptedBytes);
    }

    /// <summary>
    /// Selects the crypt filter for a stream from its dictionary and filter chain.
    /// </summary>
    internal PdfCryptFilter GetStreamCryptFilter(PdfDictionary streamDictionary, List<PdfFilterType> filters)
    {
        if (streamDictionary == null)
        {
            throw new ArgumentNullException(nameof(streamDictionary));
        }

        if (filters == null)
        {
            throw new ArgumentNullException(nameof(filters));
        }

        if (filters.Count > 0 && filters[0] == PdfFilterType.Crypt)
        {
            PdfDictionary? cryptParameters = streamDictionary.GetArray(PdfTokens.DecodeParmsKey)?.GetDictionary(0)
                ?? streamDictionary.GetDictionary(PdfTokens.DecodeParmsKey);

            return Parameters.GetCryptFilter(cryptParameters?.GetName(PdfTokens.NameKey));
        }

        PdfString? type = streamDictionary.GetName(PdfTokens.TypeKey);
        if (type == PdfTokens.EmbeddedFileKey)
        {
            return Parameters.EmbeddedFileCryptFilter;
        }

        if (type == PdfTokens.MetadataKey && !Parameters.EncryptMetadata)
        {
            return PdfCryptFilter.Identity;
        }

        return Parameters.StreamCryptFilter;
    }

    /// <summary>
    /// Authenticates the document if any of its stream, string or embedded file filters requires
    /// authentication when the document is opened.
    /// </summary>
    /// <exception cref="PdfIncorrectPasswordException">Thrown if none of the supplied passwords is correct.</exception>
    internal void AuthenticateOnOpen()
    {
        bool requiresAuthentication = RequiresAuthenticationOnOpen(Parameters.StreamCryptFilter)
            || RequiresAuthenticationOnOpen(Parameters.StringCryptFilter)
            || RequiresAuthenticationOnOpen(Parameters.EmbeddedFileCryptFilter);

        if (requiresAuthentication)
        {
            Authenticate(PdfAuthEvent.DocumentOpen);
        }
    }

    /// <summary>
    /// Validates <paramref name="password"/> as the user or owner password and, when valid, computes the file key.
    /// </summary>
    /// <returns>True when the password is valid.</returns>
    protected abstract bool TryAuthenticate(string password);

    /// <summary>
    /// Decrypts non-empty data of the object identified by <paramref name="reference"/> with an authenticated file key.
    /// </summary>
    protected abstract byte[] Decrypt(ReadOnlyMemory<byte> data, PdfReference reference, PdfCryptFilter cryptFilter);

    private static bool RequiresAuthenticationOnOpen(PdfCryptFilter cryptFilter)
    {
        return cryptFilter.Method != PdfCryptFilterMethod.None
            && cryptFilter.AuthEvent == PdfAuthEvent.DocumentOpen;
    }

    private void Authenticate(PdfAuthEvent authEvent)
    {
        if (_isAuthenticated)
        {
            return;
        }

        lock (_authenticationLock)
        {
            if (_isAuthenticated)
            {
                return;
            }

            if (TryAuthenticate(string.Empty))
            {
                _isAuthenticated = true;
                return;
            }

            if (_onPasswordRequested == null)
            {
                throw new PdfIncorrectPasswordException();
            }

            var reason = PdfPasswordRequestReason.PasswordRequired;
            while (true)
            {
                string? password = _onPasswordRequested(reason, authEvent);
                if (password == null)
                {
                    throw new PdfIncorrectPasswordException();
                }

                if (TryAuthenticate(password))
                {
                    _isAuthenticated = true;
                    return;
                }

                reason = PdfPasswordRequestReason.IncorrectPassword;
            }
        }
    }
}
