# Great file migration

Folder and namespace layout across the solution, measured against the .NET Framework Design Guidelines. Decisions are made folder by folder; nothing here is moved yet.

## Pattern

1. A namespace groups by feature, not by kind of type. `Model`, `Utilities`, `Helpers`, `Common` and `Extensions` name a kind and are not used.
2. The main public types of a feature live in the feature namespace itself (`HttpClient` is in `System.Net.Http`, not `System.Net.Http.Model`).
3. Types used together are not split across namespaces. The common scenario needs one `using`.
4. A sub-namespace holds either:
   - a large set of advanced or secondary public types, named after what they are (`System.Net.Http.Headers`, `System.Text.Json.Serialization`), or
   - internal machinery grouped by job (`Rendering`, `Decoding`, `Parsing`). Users never import these.
5. At most two levels below the feature.

Rule of thumb: a folder name that answers "what kind of type is this?" merges up; a folder name that answers "what does this do?" may stay.

References: `PdfPixel/Geometry` (model in the root, `PathOperations` by job) and `PdfPixel.Ccitt` (one namespace, no subfolders).

## Workflow per folder

1. Decide the target for the folder.
2. Move the files (Visual Studio). Moving alone still builds, since C# namespaces are not tied to folders.
3. Sync namespaces and references in one pass (Visual Studio "Sync namespaces"). Changing namespaces without updating references breaks the build, so these two always land together.
4. Build the changed projects, one feature per commit.

## Order

1. Core library (`PdfPixel`), folder by folder.
2. `PdfPixel.Color` and `PdfPixel.Fonts`, next to or right after the core `Color` and `Fonts` folders (namespace collision, see below).
3. Codec libraries, one at a time.
4. Panel projects, tools, demos, tests.

## 1. Core library (`PdfPixel`)

### Root

Target: `PdfDocumentReader`, document-wide exceptions (`PdfInvalidDocumentException`), and the document and page model. Exceptions that belong to one feature stay with that feature (`PdfAuthenticationException` → `Encryption`).

### `Models` (61 files)

Splits into four groups:

| Group | Types | Target |
|---|---|---|
| Document and page | `PdfDocument`, `PdfPage`, `IPdfDocument`, `IPdfPage`, `PdfDocumentInformation`, `PdfMetadata`, `PdfPageLayout`, `PdfPageMode`, `PdfPermissions`, `PdfVersion`, `PdfTabOrder`, `PdfMarkInformation`, `PdfSignatureFlags`, `PdfRenderingParameters`, `PageLabelStyle` | root |
| Object model | `PdfObject`, `PdfDictionary`, `PdfArray`, all `*Value` types, `PdfValue`, `PdfReference`, `PdfString`, `PdfKeyword`, `PdfOperator`, `PdfTrailer`, `PdfCrossReference`, `PdfCrossReferenceSection`, `PdfFileHeader`, `PdfFileIdentifier`, `PdfObjectInfo`, `PdfObjectStreamReference` | new namespace, e.g. `PdfPixel.Objects` (open) |
| Execution | `PdfClipOperation`, `PdfClipRectangleSource`, `PdfCommandExecutionParameters`, `IPdfExecutionObserver`, `PdfCancellationExecutionObserver` | `Commands` or `Rendering` (open) |
| Resources | `PdfPageResources`, `PdfResourceDictionary`, `PdfResourceNode`, `PdfXObject`, `PdfXObjectSubtype` | open (name collision with `Resources`, see below) |

Not yet assigned: `IPdfContentHost`, `PdfContentHost`, `IPdfDocumentInternal`, `IPdfPageInternal`, `PdfDocumentObjectCache`, `PdfDeveloperExtension`, `PdfRange`, `PdfArrayExtensions`, `PdfValueExtension`.

### Feature folders

| Today | Target |
|---|---|
| `Annotations/Model` (41 public types) | → `Annotations` |
| `Annotations/Rendering` (all internal) | stays |
| `Actions/Model` | → `Actions` |
| `Navigation/Model` | → `Navigation` |
| `Measures/Model` | → `Measures` |
| `OptionalContent/Model` | → `OptionalContent` |
| `Encryption/Model` | → `Encryption`; `Cryptography`, `Decryption` stay |
| `Content/Model` | → `Content` |
| `Fonts/Model` | → `Fonts`; `Management`, `Mapping` stay |
| `Imaging/Model` | → `Imaging`; `Decoding`, `Encoding`, `Processing` stay |
| `Shading/Model` | → `Shading`; `Builder`, `Decoding` stay |
| `Pattern/Model` | → `Pattern` |
| `Pattern/Utilities` (`PdfPatternParser`, `TilingPatternShaderBuilder`, internal) | → `Pattern` root, or a job-named sub-namespace |
| `Tagging/Model` | → `Tagging` |
| `Tagging/Model/Attributes` (33 types) | → `Tagging.Attributes`, together with `PdfStructureAttributeFactory` |
| `Transparency/Model` | → `Transparency` |
| `Transparency/Utilities` | `SoftMaskDrawingScope` is public → `Transparency`; `SoftMaskUtilities` (internal) → job name or root |
| `Commands/Model` (30 types) | → `Commands`; `Cache`, `Context`, `Image`, `Processing` stay |
| `Geometry` | done |
| `Color`, `Files`, `Forms`, `Functions`, `Streams`, `Rendering`, `Skia` | no `Model` level; review contents only |

### Open questions

- **Internal parsers.** `Pattern/Utilities` and `Annotations/Rendering` both hold parsers. Either keep internal parsers in the feature root or give each feature a `Parsing` sub-namespace; one rule everywhere.
- **`Resources`.** The folder holds embedded data (`AnnotationIcons.xml`, CMaps, `PdfResourceLoader`). The PDF resource dictionary types cannot share the name; one of the two is renamed (e.g. `EmbeddedData` for the data).
- **`Text`.** Holds `PdfTokens` and the `PdfEnum*` attributes, which are parsing infrastructure, not text.
- **`Parsing`.** Holds `PdfPageLabelResolver` and `PdfDateParser`, which are not object parsing.

## 2. `PdfPixel.Color` and `PdfPixel.Fonts`

The core library references both, and they share namespaces with it:

| Namespace | In `PdfPixel` | Also in |
|---|---|---|
| `PdfPixel.Color.Transform` | 3 files | `PdfPixel.Color` (8 files) |
| `PdfPixel.Fonts.Model` | 20 files | `PdfPixel.Fonts` (10 files) |
| `PdfPixel.Color`, `PdfPixel.Fonts` | feature folders | assembly names |

Once core `Fonts/Model` merges into `PdfPixel.Fonts`, the assembly's `PdfPixel.Fonts.Model` still remains under the old name.

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
