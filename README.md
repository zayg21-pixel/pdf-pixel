# PDF Pixel

[![NuGet](https://img.shields.io/nuget/v/PdfPixel.svg)](https://www.nuget.org/packages/PdfPixel/)
[![Publish NuGet Package](https://github.com/zayg21-pixel/pdf-pixel/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/zayg21-pixel/pdf-pixel/actions/workflows/nuget-publish.yml)
[![CI](https://github.com/zayg21-pixel/pdf-pixel/actions/workflows/ci.yml/badge.svg)](https://github.com/zayg21-pixel/pdf-pixel/actions/workflows/ci.yml)

PDF Pixel is a native C# PDF rendering library for .NET, built around SkiaSharp as its rendering engine. It renders PDF documents to raster output with full fidelity, optimized for both CPU and GPU rendering, with no dependency on external PDF frameworks — just SkiaSharp, standard Microsoft libraries, bundled Adobe CMap resources for character mapping, and the bundled URW Core 35 fonts used for the Standard 14 fonts. Coverage spans the full PDF specification for static content, with all essential rendering features supported. A ready-made panel component makes it straightforward to embed PDF viewing directly into any application.

---

## Documentation

- [PdfPixel.Examples](PdfPixel.Examples) — a console project with runnable examples: rendering a PDF page, applying ICC transforms, decoding JPEG, JPEG 2000, JBIG2 and CCITT

---

## Feature Support

### Images
- ✅ CCITT (G3, G4)
- ✅ JBIG2
- ✅ JPEG
- ✅ JPEG 2000 (JPX)

### Stream Filters
- ✅ Flate
- ✅ LZW
- ✅ ASCII Hex
- ✅ ASCII 85
- ✅ Run Length
- ✅ Predictors
- 🔲 Crypt *(planned)*

### Fonts
- ✅ Type 1 (PostScript)
- ✅ Type 3 (User-defined)
- ✅ TrueType
- ✅ OpenType
- ✅ CID Fonts

### Shading
- ✅ Type 1 — Function-based
- ✅ Type 2 — Axial
- ✅ Type 3 — Radial
- ✅ Type 4 — Free-form Gouraud triangle mesh
- ✅ Type 5 — Lattice-form Gouraud triangle mesh
- ✅ Type 6 — Coons patch mesh
- ✅ Type 7 — Tensor-product patch mesh

### Functions
- ✅ Type 0 — Sampled
- ✅ Type 2 — Exponential interpolation
- ✅ Type 3 — Stitching
- ✅ Type 4 — PostScript calculator

### Color Management
- ✅ DeviceGray, DeviceRGB, DeviceCMYK
- ✅ CalGray, CalRGB, Lab
- ✅ ICC profiles (v2, v4)
- ✅ Indexed
- ✅ Separation, DeviceN
- ✅ Pattern

### Encryption
- ✅ RC4 40-bit / 128-bit
- ✅ AES-128
- ⚠️ AES-256 *(R6 only, R5 planned)*

### Annotations
- ✅ Text
- ✅ Line, Square, Circle, Polygon, PolyLine
- ✅ Highlight, Underline, Squiggly, StrikeOut
- ✅ Ink
- ✅ Link
- ✅ Popup
- ✅ File Attachment
- ✅ Stamp
- ✅ Caret
- ⚠️ FreeText *(no default appearance (DA) generation — requires an existing appearance stream)*
- ⚠️ Redact *(rendered as generic annotation, no dedicated support)*
- 🔲 Widget (AcroForm) *(planned)*
- ❌ Sound, Movie, Screen, 3D

### Interactive & Scripting
- ⚠️ Text Selection *(extracts all glyphs, but does not sort into reading order; markup-aware selection not fully supported)*
- ❌ JavaScript *(not planned)*
- ❌ XFA *(not planned)*

---

## Release Plan

### Stage 1
- Bug fixes, complete TODOs
- Add Examples project
- Release NuGet packages:
  - `PdfPixel`
  - `PdfPixel.Color`
  - `PdfPixel.Fonts`
  - `PdfPixel.PostScript`
  - `PdfPixel.Ccitt`
  - `PdfPixel.Jpg`
  - `PdfPixel.Jpx`
  - `PdfPixel.Jbig2`

### Stage 2
- Finalize PdfPixel.PdfPanel and demo projects for WPF and WASM (Web)
- Unit tests
- Stage 2 bug fixes
- Implement text extraction and text selection
- Implement `PdfPixel.Tiff`
- Documentation
- Release NuGet packages:
  - `PdfPixel.PdfPanel`
  - `PdfPixel.PdfPanel.Wpf`
  - `PdfPixel.PdfPanel.Web`

### Stage 3
- Add AcroForm / Widget annotation support
- Add Avalonia and MAUI support
- Full test coverage

---

## License

PDF Pixel is licensed under the [MIT License](LICENSE). Bundled third-party resources keep their own licenses, each recorded next to the project that embeds it:

- [PdfPixel/NOTICE.txt](PdfPixel/NOTICE.txt)
- [PdfPixel.Fonts/NOTICE.txt](PdfPixel.Fonts/NOTICE.txt)
- [PdfPixel.Color/NOTICE.txt](PdfPixel.Color/NOTICE.txt)

Every package carries the notices for the assemblies it ships, under `notices/`.
