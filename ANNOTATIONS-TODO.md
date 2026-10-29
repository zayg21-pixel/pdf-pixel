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

## 2. Content bounds for hover areas and the bubble

Hover areas come from `Rectangle` (`GetHoverRectangles`), and the bubble anchor from `ContentStart`, a single point each type has to override. `Rectangle` often does not match what is drawn (RD insets, QuadPoints, path geometry, an oversized or empty Rect), and a missed `ContentStart` override goes unnoticed.

How: an abstract `ContentRectangle` on `PdfAnnotation`, the rectangle of what is actually drawn, replaces `ContentStart`. Several types already have a `ContentRectangle` (Square, Circle, FreeText) or an equivalent (`MarkRectangle` on Caret); they become its overrides, and every other type derives it from its fallback geometry (quadrilaterals, line, ink, polygon and polyline paths, the icon rectangle). Hover rectangles and the bubble anchor (its upper-left corner) both come from it. A content path instead of a rectangle (exact hit testing of lines and ink) would be ideal but may be too complex; decide when starting.

With an appearance stream the fallback geometry is not what is drawn. The appearance is the form's BBox mapped through its Matrix and fitted into `Rectangle` (ISO 32000-2 12.5.5), then clipped to `Rectangle`. What `ContentRectangle` is in that case has not been considered yet: the transformed BBox, `Rectangle`, or still the fallback geometry when the appearance is known to match it.

Files: `PdfPixel/Annotations/PdfAnnotation.cs`, the annotation types that override `ContentStart` or `GetHoverRectangles`, `PdfPixel.PdfPanel/Annotations/PdfAnnotationPopup.cs`, `PdfPixel.PdfPanel/Caching/PdfDocumentContentExtensions.cs`
