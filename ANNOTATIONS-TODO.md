# Annotation cleanups

Open annotation work, smallest first. Items that share work are grouped together. Items are tracked here only, with no `// TODO` in code; the listed files are where the work lands.

Widgets and the FreeText default style string are tracked in `FORMS-TODO.md`. Hide-action field targets are forms work and are not tracked here.

Test files: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (fallback rendering playground), plus the Gold entries `comments.pdf`, `highlights.pdf`, `issue14438.pdf`, `issue9552.pdf`.

## 1. Redaction annotation (view only)

`Redact` falls through to `PdfGenericAnnotation`. Add a `PdfRedactAnnotation` that models every entry of ISO 32000-2 Table 195 and renders the marked region. Applying redactions (removing content) is out of scope.

Model: QuadPoints (as text markup), IC (DeviceRGB, transparent when absent, ignored when RO is present), RO (overlay form XObject, origin at the lower-left corner of the rectangle, takes precedence over IC, OverlayText, DA and Q), OverlayText, Repeat (default false), DA (required with OverlayText, through the existing default appearance parsing) and Q. The region is QuadPoints when present, otherwise Rect.

Rendering: an appearance stream is drawn as usual. The spec only defines what the overlay looks like after the redaction is applied; what a viewer shows before that has to come from Acrobat (open questions).

Files: `PdfPixel/Annotations/PdfAnnotation.cs`, `PdfPixel/Annotations`

### Open questions (verify in Acrobat)

1. Fallback without appearance stream: Acrobat appears to outline the region in the annotation color and show the overlay (RO, or IC fill with OverlayText) on rollover. Confirm both, and whether the outline follows the quadrilaterals or the rectangle.
2. Repeat: how the repeated OverlayText is laid out and clipped within the region.
3. RO when it is also in the AP dictionary: does Acrobat ever draw RO before the redaction is applied?
