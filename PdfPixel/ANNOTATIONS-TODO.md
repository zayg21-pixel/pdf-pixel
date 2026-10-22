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
- stencil cache. Currently all fonts/shadings that are using 1bit CCITT and JBIG2 often re-evailated, that hurts performance, we can make stencil cache there. Techincally, it can be per usage, say, Type3 fonts can use it for better alignment of graphics, page can have it's own for small images. Big stencils can be rejectect by cache. Cache shall work for both image and inline image, we'd need to distinguish index in content stream for inlines. Images stored in original resolution and only decoding step is skipped. Not yet descided who will own the cache and how it would be propagated, probably via image target in ImageDecodingContext. Image cache implementation can then go to context and stored on document level. Even in initial implementation cache can already be universal for images, not only "stencil".
- yield between tile renders