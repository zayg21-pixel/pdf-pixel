# Annotation cleanups

Open annotation work, smallest first. Items that share work are grouped together. Each item is also a `// TODO` in the listed files.

FreeText fallback rendering and Hide-action field targets are forms work and are not tracked here.

Test files: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (fallback rendering playground), plus the Gold entries `comments.pdf`, `highlights.pdf`, `issue14438.pdf`, `issue9552.pdf`.

## 1. Line captions

Text laid out along the line:

- whether the Contents text is shown as a caption on the line (`Cap`)
- caption position, centered inside the line or on top of it (`CP`)
- caption offset from its default position (`CO`)

File: `PdfPixel/Annotations/PdfLineAnnotation.cs`

## 2. View-dependent flags

Rendering does not know the view zoom or the viewer's rotation. Both have to be passed in through the rendering parameters, then from the panel. Hover rectangles have to follow the same view state.

- **NoZoom**: keep a fixed device-pixel size across zoom levels. Today annotations scale with the page.
- **NoRotate**: stay upright, pivoting on the upper-left corner of the annotation rectangle. The rotation to cancel is the page's `/Rotate` plus the viewer's own rotation.
- **ToggleNoView**: show a `NoView` annotation while the pointer is over it or it is selected.
Files: `PdfPixel/Annotations/PdfAnnotationFlags.cs`, `PdfPixel/PdfRenderingParameters.cs`, `PdfPixel.PdfPanel`

## 3. Widgets (prototype)

Prototype bringing widget annotations back:

- **Rendering**: a widget without an appearance stream is drawn from its field's default appearance (DA) as a fallback.
- **Editing**: the panel provides MVVM view models for drawing native controls on top of the document, through which the user edits a field's value (V).
- **Storage**: edited values are kept in memory only; nothing is written back to the file.

Files: `PdfPixel/Annotations/PdfWidgetAnnotation.cs`, `PdfPixel/Annotations/PdfAnnotation.cs`, `PdfPixel/Forms/PdfFormField.cs`, `PdfPixel.PdfPanel/Mvvm`
