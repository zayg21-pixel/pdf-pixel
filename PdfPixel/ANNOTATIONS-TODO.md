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

### Panel thumbnails: limit and cancel semantics

Needs its own investigation before any fix.

- `thumbnailMaximumCount` is likely broken: check whether `EnforceThumbnailLimit` actually bounds the context's thumbnail cache under preload plus explicit requests.
- `PdfThumbnailViewModel` keeps its own copy of the PNG (`_png`), a second store beside the context cache that the context's limit does not govern.
- Cancel only cancels a pending request and keeps the view model's PNG. Releasing thumbnails needs its own Clear-shaped API, separate from Cancel.

Files: `PdfPixel.PdfPanel/Thumbnails/PdfPanelThumbnails.cs`, `PdfPixel.PdfPanel/Scheduling/PdfPanelScheduler.Thumbnails.cs`, `PdfPixel.PdfPanel/Mvvm/PdfThumbnailViewModel.cs`, `PdfPixel.PdfPanel.Web/PdfPanelInterop.cs`