# Annotation cleanups

Open annotation work, smallest first. Items that share work are grouped together. Each item is also a `// TODO` in the listed files.

Widgets, default appearance (DA) handling, FreeText fallback rendering and Hide-action field targets are forms work and are not tracked here.

Test files: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (fallback rendering playground), plus the Gold entries `comments.pdf`, `highlights.pdf`, `issue14438.pdf`, `issue9552.pdf`.

## 1. Single-key parses

No new mechanism; each is one key read and used where the annotation is drawn or hit-tested.

- **Invisible flag scope**: `Invisible` applies only to annotation types the viewer does not recognize. Today the panel skips every annotation with the flag.
  `PdfPixel.PdfPanel/ContentProvider/PdfDocumentContentExtensions.cs`

## 2. Annotation base keys

Apply to every annotation type.

- **Optional content**: hide the annotation when its layer is off (`OC`). `PdfOptionalContentMembership` already reads the key for XObjects.
- **Blend mode**: the mode the appearance is composited onto the page with, PDF 2.0 (`BM`).

Files: `PdfPixel/Annotations/Model/PdfAnnotationBase.cs`, `PdfPixel/Annotations/Rendering/PdfAnnotationAppearanceRenderer.cs`

## 3. Line captions

Text laid out along the line:

- whether the Contents text is shown as a caption on the line (`Cap`)
- caption position, centered inside the line or on top of it (`CP`)
- caption offset from its default position (`CO`)

File: `PdfPixel/Annotations/Model/PdfLineAnnotation.cs`

## 4. Measurement

One shared measurement model (`Measure`: units and scale), used by the dimension annotations:

- Line: `IT /LineDimension`
- Polygon: `IT /PolygonDimension`
- PolyLine: `IT /PolyLineDimension`

Files: `PdfPixel/Annotations/Model/PdfLineAnnotation.cs`, `PdfPixel/Annotations/Model/PdfPolygonAnnotation.cs`, `PdfPixel/Annotations/Model/PdfPolyLineAnnotation.cs`, plus a new model type.

## 5. View-dependent flags

Rendering does not know the view zoom or the viewer's rotation. Both have to be passed in through the rendering parameters, then from the panel.

- **NoZoom**: keep a fixed device-pixel size across zoom levels. Today annotations scale with the page.
- **NoRotate**: stay upright, pivoting on the upper-left corner of the annotation rectangle. The rotation to cancel is the page's `/Rotate` plus the viewer's own rotation.
- **ToggleNoView**: show a `NoView` annotation while the pointer is over it or it is selected.

Files: `PdfPixel/Annotations/Model/PdfAnnotationFlags.cs`, `PdfPixel/Models/PdfRenderingParameters.cs`, `PdfPixel.PdfPanel`
