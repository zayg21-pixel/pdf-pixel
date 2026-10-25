# Great file migration

Folder and namespace layout across the solution, measured against the .NET Framework Design Guidelines. `PdfPixel` and `PdfPixel.Fonts` follow it; the projects listed under "Remaining work" do not yet.

## Rules

1. A namespace groups by feature, not by kind of type. `Model`, `Models`, `Utilities`, `Helpers`, `Common` and `Extensions` name a kind and are not used.
2. The main public types of a feature live in the feature namespace itself (`HttpClient` is in `System.Net.Http`, not `System.Net.Http.Model`).
3. Types used together are not split across namespaces. The common scenario needs one `using`.
4. A sub-namespace holds either:
   - a large set of advanced or secondary public types, named after what they are (`System.Net.Http.Headers`), or
   - internal machinery grouped by job (`Rendering`, `Decoding`, `Parsing`). Users never import these.
5. At most two levels below the feature.
6. There is no size rule. A small namespace stays when it is a standalone feature or readable internal machinery; it is never merged only for being small.
7. A project root holds only its central model or general access: the document and page model in `PdfPixel`, typeface access in `PdfPixel.Fonts`. Everything else is a feature or a sub-feature.
8. A type goes to the feature it describes, not to the dictionary it is stored in (`PdfPermissions` → `Encryption`, output intents → `Color`).
9. Base classes carry no `Base` suffix and are also the factory (`PdfAction.GetAction`, `PdfFont.GetFont`). A factory that is large on its own may stay a separate class.
10. A support assembly uses the namespace of the core library feature it serves. Every type lives in exactly one assembly.
11. In a support assembly, a format folder holds that format's model and parser; conversions stay with the source format. The root holds general access to the formats.
12. Shared primitives get a feature folder named after what they are, mirroring the core library where it has one (`Geometry`, `EmbeddedData`).

Rule of thumb: a folder name that answers "what kind of type is this?" merges up; one that answers "what does this do?" may stay.

Before moving a type, check its inheritance chain and who uses it, not only its name.

References: `PdfPixel/Geometry` (model in the root, `PathOperations` by job), `PdfPixel.Fonts` (access in the root, one folder per format), `PdfPixel.Ccitt` (one namespace, no subfolders).

## How to move a folder

1. Decide the target for the folder against the rules.
2. Move the files (Visual Studio). Moving alone still builds, since C# namespaces are not tied to folders.
3. Sync namespaces and references in one pass (Visual Studio "Sync namespaces"). Changing namespaces without updating references breaks the build, so the two always land together.
4. Fix fully qualified `cref`s and the names of embedded resources: a resource's manifest name follows its folder path, so renaming a resource folder means updating the csproj globs and the loader that builds the name.
5. Build the changed projects and their consumers, one feature per commit.

Visual Studio runs "Sync namespaces" once per target framework and merges the results, leaving `<<<<<<< TODO: Unmerged change` blocks where the runs disagree. Set `<TargetFrameworks>` in `Directory.Build.props` to a single framework while syncing and restore the full list afterwards.

## Remaining work

Order: `PdfPixel.Color`, then the codec libraries one at a time, then panels, then tools, demos and tests.

### `PdfPixel.Color`

- `Icc/Model`, `Icc/Utilities` and `Structures` are kind folders: merge them into their features.

### Codec libraries

`PdfPixel.Jpg`, `PdfPixel.Jpx`, `PdfPixel.Png`, `PdfPixel.Tiff` and `PdfPixel.Jbig2` use `Model` / `Parsing` / `Decoding` with empty root namespaces.

- The codec name is the feature: decoder entry points and the public model move from `Model` to `PdfPixel.<Codec>`.
- `Parsing`, `Decoding`, `Huffman` and `Idct` stay as internal machinery. Whether advanced public types keep a sub-namespace is decided per codec.
- `PdfPixel.Jpg/PdfPixel.Jpg (netstandard2.0)/Huffman/JpgHuffmanTableExtensions.cs` is compiled (`JpgHuffmanTable.cs` calls its `WithCounts`). Move it to `PdfPixel.Jpg/Huffman` and delete the `(netstandard2.0)` folder.

### Panels

- `PdfPixel.PdfPanel`: rename `ContentProvider`, which holds `PdfPanelDocument` (storage); merge the `Extensions` kind folder; delete the empty `Images` and `UserInterface` folders; check whether `Requests`, `Execution` and `WorkQueue` are one feature.
- `PdfPixel.PdfPanel.Web`: `Dto/Model` → `PdfPixel.PdfPanel.Web.Dto`. `Dto/Converters`, `Emscripten` and `Rendering` stay.
- `PdfPixel.PdfPanel.Wpf`: no change.

### Tools, demos, tests

Applications, not public APIs; changed for consistency only.

- Merge the `Utilities` kind folders in `PdfPixel.Gold`, `PdfPixel.PanelDiagnostics` and `PdfPixel.Demo.Wpf`.
- `PdfPixel.Demo.Console`: namespace `PdfPixel.Console.Demo` is reversed, and `Console` shadows `System.Console`. Pick a name that does neither.
- `PdfPixel.Benchmarks`: namespace `Benchmarks` → `PdfPixel.Benchmarks`.
- `PdfPixel.Tests`: lowercase test data folders (`jbig2`, `jpx`, `png`, `tiff`) sit next to code folders of the same names; decide whether to separate them.
