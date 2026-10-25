# Great file migration

Folder and namespace layout across the solution, measured against the .NET Framework Design Guidelines. Decisions are made folder by folder. The core library (`PdfPixel`) is done; the support assemblies, codecs, panels and tools are still open.

## Pattern

1. A namespace groups by feature, not by kind of type. `Model`, `Utilities`, `Helpers`, `Common` and `Extensions` name a kind and are not used.
2. The main public types of a feature live in the feature namespace itself (`HttpClient` is in `System.Net.Http`, not `System.Net.Http.Model`).
3. Types used together are not split across namespaces. The common scenario needs one `using`.
4. A sub-namespace holds either:
   - a large set of advanced or secondary public types, named after what they are (`System.Net.Http.Headers`, `System.Text.Json.Serialization`), or
   - internal machinery grouped by job (`Rendering`, `Decoding`, `Parsing`). Users never import these.
5. At most two levels below the feature.

Rule of thumb: a folder name that answers "what kind of type is this?" merges up; a folder name that answers "what does this do?" may stay.

Rules settled during the core library pass:

- There is no size rule. A small namespace stays when it is a standalone feature (`Commands.Cache`) or readable internal machinery (`Imaging.Encoding`); it is never merged only for being small.
- The `PdfPixel` root holds only the document and page model. Everything else is a feature or a sub-feature.
- A type goes to the feature it describes, not to the dictionary it is stored in (`PdfPermissions` → `Encryption`, `PdfTabOrder` → `Annotations`, output intents → `Color`).
- Inheritance follows the `PdfAction` shape: the base class carries no `Base` suffix and is also the factory (`PdfAction.GetAction`, `PdfFont.GetFont`, `PdfDecryptor.GetDecryptor`). A factory that is large on its own may stay a separate class (`PdfStructureAttributeFactory`).

References: `PdfPixel/Geometry` (model in the root, `PathOperations` by job) and `PdfPixel.Ccitt` (one namespace, no subfolders).

## Workflow per folder

1. Decide the target for the folder.
2. Move the files (Visual Studio). Moving alone still builds, since C# namespaces are not tied to folders.
3. Sync namespaces and references in one pass (Visual Studio "Sync namespaces"). Changing namespaces without updating references breaks the build, so these two always land together.
4. Build the changed projects, one feature per commit.

Visual Studio runs "Sync namespaces" once per target framework and merges the results, leaving `<<<<<<< TODO: Unmerged change` blocks in files where the runs disagree. Set `<TargetFrameworks>` in `Directory.Build.props` to a single framework while syncing and restore the full list afterwards.

## Order

1. Core library (`PdfPixel`), folder by folder. **Done.**
2. `PdfPixel.Color` and `PdfPixel.Fonts` (namespace collision, see below).
3. Codec libraries, one at a time.
4. Panel projects, tools, demos, tests.

## 1. Core library (`PdfPixel`) — done

All `Model`, `Models` and `Utilities` folders are gone. The core library, the panel libraries and every tool, demo and test project build with no warnings or errors, and the short Gold run matches.

### Final layout

| Namespace | Holds |
|---|---|
| `PdfPixel` | `PdfDocumentReader`, `PdfInvalidDocumentException`, document and page model (`PdfDocument`, `PdfPage`, their interfaces, `PdfDocumentInformation`, `PdfMetadata`, `PdfMarkInformation`, `PdfVersion`, `PdfDeveloperExtension`, `PdfPageLayout`, `PdfPageMode`, `PageLabelStyle`, `PdfPageLabelResolver`, `PdfRenderingParameters`), content hosts |
| `Objects` | object model (`PdfObject`, `PdfValue` and every `*Value`, `PdfDictionary`, `PdfArray`, `PdfReference`, `PdfString`, `PdfRange`), file structure (header, trailer, cross-reference, file identifier), `PdfEnum*` attributes, `PdfEncodingExtensions` |
| `Parsing` | parser, cross-reference and object-stream parsers, content-stream tokens, `PdfTokens` |
| `Resources` | resource dictionaries and XObjects |
| `EmbeddedData` | embedded data (`AnnotationIcons.xml`, CMaps, `PdfResourceLoader`, `PdfTextResourceConverter`) |
| `Actions`, `Annotations`, `Measures`, `Navigation`, `OptionalContent`, `Tagging`, `Transparency`, `Pattern` | model in the feature root |
| `Annotations.Rendering`, `Tagging.Attributes` | sub-namespaces |
| `Color` | `PdfColor`, `PdfColors`, output intents, `PdfRenderingIntent`; sub-namespaces `ColorSpace`, `Paint`, `Transform` (all internal) |
| `Commands` | commands, processors, execution observers and parameters; sub-namespaces `Cache`, `Execution`, `Image`, `Processing` |
| `Content` | content tree, `PdfContentTreeBuilder`, content extraction |
| `Text` | text extraction (`PdfTextExtractor`, `PdfWord`, …), text showing (`PdfText`, shaped glyphs), `PdfRichTextContentParser` |
| `Encryption` | credentials, crypt filters, security handler, `PdfPermissions`; sub-namespaces `Cryptography`, `Decryption` (with `PdfDecryptorParameters`) |
| `Fonts` | font model, `PdfCharacterCode`, `PdfStandardFontName`, `PdfFontEncodingInfo`; sub-namespaces `Mapping`, `Substitution` |
| `Imaging` | image model; sub-namespaces `Decoding`, `Encoding`, `Processing` |
| `Shading` | shading model, `PdfShadingImage`; sub-namespaces `Builder`, `Decoding` |
| `Rendering` | renderers, `PdfGraphicsState`, `PdfGraphicsStateParameters`; sub-namespaces `Form`, `Image`, `Operators`, `Path`, `Shading`, `Text` |
| `Streams` | stream filters, `PdfObjectStream`, `PdfStreamDecoder`, `PdfStreamFilter`, the per-filter `PdfDecodeParameters` types |
| `Files`, `Forms`, `Functions`, `Geometry`, `Skia` | unchanged |

### Renames made along the way

- `PdfFontBase` → `PdfFont` (absorbs `PdfFontFactory`), `BasePdfDecryptor` → `PdfDecryptor` (absorbs `PdfDecryptorFactory`), `PdfStructureAttributeBase` → `PdfStructureAttribute`.
- `FunctionShadingResult` → `PdfShadingImage`, `PdfLinearGradient` → `PdfAxialGradient`.
- `Fonts/Management` → `Fonts/Substitution`, `Commands/Context` → `Commands/Execution`.
- Pattern: `PatternMatrix` → `Matrix`, `CellResources` → `Resources`, `PaintTypeKind` → `PaintType`, `TilingTypeKind` → `TilingType`, `PdfTilingSpacingType` → `PdfTilingType`, `TilingPatternShaderBuilder` → `TilingPatternCellRenderer`. `PaintType` and `TilingType` are nullable; `Unknown` is never produced.

### Resolved questions

- **Internal parsers** stay with their feature (`PdfPatternParser` in the `Pattern` root, annotation parsers in `Annotations.Rendering`).
- **`Resources`** now holds the PDF resource dictionaries; the embedded data moved to `EmbeddedData`.
- **`Text`** holds text extraction and text showing; `PdfTokens` moved to `Parsing`, the `PdfEnum*` types to `Objects`.
- **`Parsing`** holds only parsing; `PdfPageLabelResolver` moved to the root. `PdfDateParser` stays: parsing the date format is parsing.

### Placed differently from the first proposal

- `PdfSignatureFlags` is in `Forms`.

### Open correction

- `IPdfExecutionObserver` was placed in `Commands` as if it were command-specific. Stream decoding, image decoding, shading building, annotation rendering and the panel all take it, so it is one of the most general types in the library. It moves to the `PdfPixel` root together with its implementation `PdfCancellationExecutionObserver`, as an exception to the document-and-page rule.

## 2. `PdfPixel.Color` and `PdfPixel.Fonts`

The core library references both, and they share namespaces with it:

| Namespace | In `PdfPixel` | Also in |
|---|---|---|
| `PdfPixel.Color.Transform` | 3 files, all internal | `PdfPixel.Color` (8 files) |
| `PdfPixel.Fonts.Model` | none (merged into `PdfPixel.Fonts`) | `PdfPixel.Fonts` (10 files) |
| `PdfPixel.Color`, `PdfPixel.Fonts` | feature folders | assembly names |

Options:

- Rename the support assemblies' namespaces after what they contain: `PdfPixel.Fonts` covers Cff, Sfnt and Type1 font file formats; `PdfPixel.Color` is mostly ICC.
- Or keep shared namespaces deliberately, with every type in exactly one assembly and a clear rule for which goes where.

Inside the projects:

- `PdfPixel.Color`: `Icc/Model`, `Icc/Utilities` and `Structures` are kind folders.
- `PdfPixel.Fonts`: `Model` is a kind folder.
- Both expose many public types (50 in `Fonts.Sfnt`, 24 in `Fonts.Cff`). If the core library is the only consumer, many could become internal.

## 3. Codec libraries

`PdfPixel.Jpg`, `PdfPixel.Jpx`, `PdfPixel.Png`, `PdfPixel.Tiff`, `PdfPixel.Jbig2` share one layout: `Model` / `Parsing` / `Decoding`, plus `Encoding`, `Streams`, `Readers`, `Huffman`, `Idct`, `Color` where needed. The root namespaces are empty (Jpx has one file).

Public types per namespace:

| Project | Root | `Model` | `Decoding` | `Parsing` | Other |
|---|---|---|---|---|---|
| Jpg | 0 | 8 | 6 | — | `Color` 2, `Huffman` 1, `Readers` 1 |
| Jpx | 0 | 13 | 4 | 1 | — |
| Png | 0 | 6 | 2 | 2 | `Encoding` 1 |
| Tiff | 0 | 11 | 3 | 3 | `Streams` 4 |
| Jbig2 | 0 | 4 | 7 | — | — |
| Ccitt | 7 | — | — | — | — |

Target: the codec name is the feature. Decoder entry points and the public model → `PdfPixel.<Codec>`. `Parsing`, `Decoding`, `Huffman`, `Idct` stay as internal machinery. Advanced public types may keep a sub-namespace, decided per codec.

Stray file: `PdfPixel.Jpg/PdfPixel.Jpg (netstandard2.0)/Huffman/JpgHuffmanTableExtensions.cs` is tracked and compiled (`JpgHuffmanTable.cs:82` calls its `WithCounts`). It belongs in `PdfPixel.Jpg/Huffman`, and the `(netstandard2.0)` folder goes.

## 4. Panel projects

### `PdfPixel.PdfPanel`

Already organized by feature, no `Model` folder.

- `ContentProvider` is named after a role that no longer exists; it holds `PdfPanelDocument` (storage).
- `Extensions` is a kind folder.
- `Images` and `UserInterface` are empty folders.
- `Requests`, `Execution` and `WorkQueue` are small and may be one feature (contents not checked).

### `PdfPixel.PdfPanel.Web`

`Dto/Model` (32 types): `Dto` is already the feature, so → `PdfPixel.PdfPanel.Web.Dto`. `Dto/Converters`, `Emscripten`, `Rendering` stay.

### `PdfPixel.PdfPanel.Wpf`

Fine as is.

## 5. Tools, demos, tests

- `Utilities` kind folders in `PdfPixel.Gold` (6 files), `PdfPixel.PanelDiagnostics` (3) and `PdfPixel.Demo.Wpf` (1). Applications, not public APIs; changed only for consistency.
- `PdfPixel.Demo.Console` uses namespace `PdfPixel.Console.Demo` (reversed; `Console` also shadows `System.Console`).
- `PdfPixel.Benchmarks` uses namespace `Benchmarks` (no `PdfPixel.` prefix).
- `PdfPixel.Tests`: namespaces are fine (`PdfPixel.Tests.Imaging.*`); lowercase test data folders (`jbig2`, `jpx`, `png`, `tiff`) sit next to the code folders of the same names.
- No code, nothing to decide: `PdfPixel.Demo.Web`, `PdfPixel.Experiment`, `PdfPixel.Jpg.Test`.
