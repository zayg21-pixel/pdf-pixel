# Annotation cleanups

Open annotation work, smallest first. Items that share work are grouped together. Each item is also a `// TODO` in the listed files.

Widgets, default appearance (DA) handling, FreeText fallback rendering and Hide-action field targets are forms work and are not tracked here.

Test files: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (fallback rendering playground), plus the Gold entries `comments.pdf`, `highlights.pdf`, `issue14438.pdf`, `issue9552.pdf`.

## 1. Line captions

Text laid out along the line:

- whether the Contents text is shown as a caption on the line (`Cap`)
- caption position, centered inside the line or on top of it (`CP`)
- caption offset from its default position (`CO`)

File: `PdfPixel/Annotations/Model/PdfLineAnnotation.cs`

## 2. View-dependent flags

Rendering does not know the view zoom or the viewer's rotation. Both have to be passed in through the rendering parameters, then from the panel. Hover rectangles have to follow the same view state.

- **NoZoom**: keep a fixed device-pixel size across zoom levels. Today annotations scale with the page.
- **NoRotate**: stay upright, pivoting on the upper-left corner of the annotation rectangle. The rotation to cancel is the page's `/Rotate` plus the viewer's own rotation.
- **ToggleNoView**: show a `NoView` annotation while the pointer is over it or it is selected.
Files: `PdfPixel/Annotations/Model/PdfAnnotationFlags.cs`, `PdfPixel/Models/PdfRenderingParameters.cs`, `PdfPixel.PdfPanel`

## Random

- Check name consistency: every property name in the public API expanded, matching C# property naming conventions.
- Analyze moving models from `Model` folders to the root and parsers into subfolders instead, matching the common rules for where models are usually stored.
- `PdfDecodeParameters` is one super model holding the `/DecodeParms` entries of every filter. Split it per usage with proper inheritance, one type per filter. Its summary is also wrong: `/DecodeParms` belongs to any stream's filters, not only to image streams.
  File: `PdfPixel/Streams/PdfDecodeParameters.cs`
- Drop the renderer interfaces (`IPdfRenderer`, `IFormRenderer`, `IImageRenderer`, `IPathRenderer`, `IShadingRenderer`, `IPdfTextRenderer`). They were meant for testing during early implementation; each has a single implementation and nothing substitutes them.
  Folder: `PdfPixel/Rendering`
- Probably make the renderer interfaces (`I*Renderer`) internal, whether or not they are dropped. Nothing outside the core library uses them or their implementations.
  Folder: `PdfPixel/Rendering`
