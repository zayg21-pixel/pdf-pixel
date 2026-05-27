# PDF Pixel

PDF Pixel is a native C# PDF rendering library for .NET, built around SkiaSharp as its rendering engine. It renders PDF documents to raster output with full fidelity, optimized for both CPU and GPU rendering, with no dependency on external PDF frameworks — just SkiaSharp, standard Microsoft libraries, and bundled Adobe CMap resources for character mapping. Coverage spans the full PDF specification for static content, with all essential rendering features supported. A ready-made panel component makes it straightforward to embed PDF viewing directly into any application.

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
- 🔲 AES-256 *(planned)*

### Annotations
- ✅ Text
- ✅ FreeText
- ✅ Line, Square, Circle, Polygon, PolyLine
- ✅ Highlight, Underline, Squiggly, StrikeOut
- ✅ Ink
- ✅ Link
- ✅ Popup
- ✅ File Attachment
- 🔲 Widget (AcroForm) *(planned)*
- ❌ Sound, Movie, Screen, 3D
- ❌ Stamp, Caret, Redact

### Interactive & Scripting
- ❌ JavaScript *(not planned)*
- ❌ XFA *(not planned)*

---

## Release Plan

### Stage 1
- Finalize PdfPixel with documentation
- Bug fixes, complete TODOs
- Add Examples project
- Release NuGet packages:
  - `PdfPixel`
  - `PdfPixel.Color`
  - `PdfPixel.Ccitt`
  - `PdfPixel.Jpg`
  - `PdfPixel.Jpx`
  - `PdfPixel.Jbig2`

### Stage 2
- Finalize PdfPixel.PdfPanel and demo projects for WPF and WASM (Web)
- Start working on unit tests
- Stage 2 bug fixes
- Migrate to SkiaSharp 4
- Implement `PdfPixel.Tiff`
- Release NuGet packages:
  - `PdfPixel.PdfPanel`
  - `PdfPixel.PdfPanel.Wpf`
  - `PdfPixel.PdfPanel.Web`

### Stage 3
- Add AcroForm / Widget annotation support
- Add Avalonia and MAUI support
- Full test coverage
