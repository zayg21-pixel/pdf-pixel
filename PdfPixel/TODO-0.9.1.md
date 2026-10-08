# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores. Split into **information** (data to expose, no effect on
what a viewer does) and **behavior** (instructions to a viewer: how to open, show or print).

## Document — information

| Entry | Location | Contents |
|---|---|---|
| Header version | `%PDF-1.x` | Version the file declares |
| `/Version` | Catalog | Overrides the header version (incremental updates) |
| `/Extensions` | Catalog | Developer extension levels, e.g. `/ADBE` `BaseVersion` + `ExtensionLevel` |
| `/Info` | Trailer | Title, Author, Subject, Keywords, Creator, Producer, CreationDate, ModDate, Trapped |
| `/Metadata` | Catalog | XMP metadata stream |
| `/ID` | Trailer | File identifier pair (read today only for decryption) |
| `/Lang` | Catalog | Document language |
| `/MarkInfo` | Catalog | Marked, UserProperties, Suspects |
| `/Outlines` | Catalog | Bookmark tree |
| `/Threads` | Catalog | Article threads |
| `/Names` | Catalog | Everything except `/Dests`: EmbeddedFiles, JavaScript, AP, Pages, Templates, IDS, URLS |
| `/AF` | Catalog | Associated files |
| `/Collection` | Catalog | Portfolio |
| `/Perms` | Catalog | DocMDP / UR3 permission signatures |
| `/Legal` | Catalog | Legal attestation |
| `/Requirements` | Catalog | Features a viewer must support |
| `/DSS` | Catalog | Document security store (signature validation data) |
| `/DPartRoot` | Catalog | Document parts hierarchy |
| `/PieceInfo` | Catalog | Application private data |
| `/SpiderInfo` | Catalog | Web capture information |

## Document — behavior

| Entry | Location | Controls |
|---|---|---|
| `/PageMode` | Catalog | UseNone, UseOutlines, UseThumbs, FullScreen, UseOC, UseAttachments |
| `/PageLayout` | Catalog | SinglePage, OneColumn, TwoColumnLeft/Right, TwoPageLeft/Right |
| `/OpenAction` | Catalog | Initial destination (page + zoom), or action run on open (e.g. JavaScript `print()`, Named `/Print`) |
| `/AA` | Catalog | Event actions: WC (will close), WS (will save), DS (did save), WP (will print), DP (did print) |
| `/URI` | Catalog | Base URI for relative links |
| `/AcroForm` | Catalog | NeedAppearances, DR, DA, Q — affects rendering of widgets without appearance streams |
| `/NeedsRendering` | Catalog | XFA form must be rendered by the viewer |

### `/ViewerPreferences` (catalog)

| Group | Keys |
|---|---|
| Window | HideToolbar, HideMenubar, HideWindowUI, FitWindow, CenterWindow, DisplayDocTitle |
| Presentation | NonFullScreenPageMode, Direction |
| Print | PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies |
| Deprecated (2.0) | ViewArea, ViewClip, PrintArea, PrintClip |
| Other | Enforce |

## Page — information

| Entry | Contents |
|---|---|
| `/UserUnit` | Size of a default user space unit (1/72 inch multiplier) — affects page size |
| `/StructParents` | Key into the structure parent tree |
| `/Thumb` | Thumbnail image |
| `/B` | Beads of article threads on the page |
| `/LastModified` | Modification date |
| `/Metadata` | Page XMP metadata stream |
| `/PieceInfo` | Application private data |
| `/BoxColorInfo` | Display colors for page boundaries |
| `/SeparationInfo` | Separation page information |
| `/ID`, `/PZ` | Web capture identifier and preferred zoom |
| `/VP` | Viewports (measurement, geospatial) |
| `/AF`, `/DPart` | Associated files, document part |

## Page — behavior

| Entry | Controls |
|---|---|
| `/Trans` | Presentation transition effect (Split, Blinds, Box, Wipe, Dissolve, Glitter, Fly, Push, Cover, Uncover, Fade) |
| `/Dur` | Seconds the page is shown before auto-advancing in presentation mode |
| `/AA` | O (page opened), C (page closed) actions |
| `/Tabs` | Annotation tab order: R, C, S, A, W |
| `/TemplateInstantiated` | Named page created from a template |
| `/PresSteps` | Sub-page navigation steps |

## Fixes and rework

| Item | Location | Work |
|---|---|---|
| Soft mask text | `Transparency/Utilities/SoftMaskUtilities.cs` | Text shown by the mask form is extracted like page text, in both rendering and text extraction |
| Credential and encryption layout | `PdfCredential.cs` | Move `PdfCredential` to encryption; rework encryption to match the other folders' structure |
| Object cache | `Models/PdfDocumentObjectCache.cs` | Use a single `PdfReference` cache; cache other big shared objects, such as CMaps |
| Streaming decryption | `Encryption/BasePdfDecryptor.cs` | Decrypt streams chunk by chunk through a stream wrapper instead of buffering the whole stream; see below |

### Streaming decryption

`DecryptStream` currently copies the whole stream into memory, decrypts the array and returns a new
`MemoryStream`. Decryption becomes a forward-only stream wrapper that can be chained with the decode
streams; strings, being small and far less common, go through the same wrapper over a `MemoryStream`.

**Ciphers (`Encryption/Cryptography`)**

- `Rc4` — sealed instance class: key schedule in the constructor,
  `Transform(ReadOnlySpan<byte> source, Span<byte> destination)` keeps `state`, `i`, `j` between calls.
- `AesCbc` decryption — instance taking key and IV: round keys expanded and inverted once (`uint[]` field),
  `DecryptBlocks(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)` on whole blocks, carrying the previous
  ciphertext block between calls. No padding handling in the cipher.
- `AesCbc.Encrypt`, `Md5`, `Sha256`, `Sha512` — unchanged; they only run on small key-derivation inputs.
- Open: the one-shot calls in key derivation (`Rc4.Transform` in R2/R3R4, `AesCbc.Decrypt` in R5R6 file key
  unwrap). Either keep static one-shot methods implemented on the instances, or migrate those callers to the
  instances directly.

**Streams (`Encryption`)**

- `Rc4DecryptStream` — read a chunk, transform, return.
- `AesCbcDecryptStream` — read the 16-byte IV, decrypt whole blocks, always hold back the last decrypted block;
  at end of source strip PKCS#7 padding from the held block. A trailing partial block is dropped. No length
  validation up front.
- Both forward-only (`CanSeek` false, `Length` throws) and own their source (`leaveOpen: false`), like the
  decode streams — the `SubrangeReadOnlyStream` from `PdfObjectStream.GetRawStream` is then disposed with the chain.

**`BasePdfDecryptor`**

- `Decrypt(Stream source, PdfReference reference, PdfCryptFilter cryptFilter, byte[] fileKey)` returns the
  matching stream; per-object key derivation unchanged (AESV2 salted, AESV3 file key, RC4 unsalted).
- `DecryptStream` returns that stream directly.
- `DecryptString` wraps the bytes in a `MemoryStream`, reads the decrypt stream to the end and returns the array.
