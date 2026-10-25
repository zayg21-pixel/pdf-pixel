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
- Check constructors/methods inconsistencies. All that created from PdfObject/Array shall be internal, same for constructor, object parsing can go to constructors instead of get/set
- instead of hardcoded values add parameters to max outline from stencil and shall we cache path or not
(maybe drop path cache)
- currently cache is bad, it shall not be pure "inline image cache" as it's for normal and inline images and path generated from 1 bit images