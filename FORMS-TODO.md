# Forms and default appearance

Default appearance (DA) support for FreeText and widget annotations, then form fields one control type at a time.

Scope: FreeText and widget annotations only. Redaction annotations are out of scope. Full rich text is out of scope.

Test file: `PdfPixel.Tests/AnnotationFallbackTest.pdf`.

## Terms

- **DA (default appearance)**: a string of content stream operators, for example `/Helv 12 Tf 0 0 1 rg`. It may hold only graphics state and text state operators (the ones allowed inside a text object); the font operator (`Tf`) is required. The grammar is the same wherever DA appears: form, field, FreeText. It is not a dictionary and has no per-annotation vocabulary.
- **AP (appearance stream)**: the annotation's own drawing, a form XObject per look (normal, rollover, down), with named states for check boxes and radio buttons.
- **V (value)**: the field's current value.

Prefer full names over PDF key acronyms in code, comments and docs; DA, AP and V are the only acronyms used freely.

## Rules

### Two render paths

- An annotation with an AP is drawn from the AP. Neither DA nor V changes how an AP is drawn; for check boxes and radio buttons the state inside the AP is picked by the appearance state (`AS`), not by V.
- Without an AP, or when the form sets "need appearances" (`NeedAppearances`, deprecated in PDF 2.0 but still read), the annotation is drawn by a fallback renderer.
- AP is interactive: it carries its own rollover and down looks. Fallback is static: one look, used for static rendering and printing. Editing is planned as native controls (WPF/MAUI/Web) drawn on top of the page, which hide the fallback. A fallback can be made interactive later, but that is not a goal now.
- Fallback rendering is not spec-defined: the spec gives no strict rules for drawing a control from DA and V, so fallback output is an approximation, not a conformance target.

### Interactive form model and default resources

- There is no model of the interactive form dictionary (`AcroForm`) yet; only the signature flags are read, directly in `PdfDocument`. A new `PdfInteractiveForm` owns the document-level form data: default resources, document-wide DA and text alignment, "need appearances", the root fields, and the signature flags (moved from `IPdfDocument`). `PdfForm` is already the form XObject, hence the spec's full name.
- Default resources (`DR`) are an ordinary resource dictionary, the same type as page resources: a `PdfResourceNode` over a `PdfResourceDictionary`, with no parent. Unlike page resources they are not inherited (no `PdfPageResources`) and belong to no content stream: they are the pool DA names (fonts, ExtGStates) resolve against when a fallback is drawn. A fallback never uses the page's resources, and an AP never uses the default resources.

### Default appearance model

- `PdfDefaultAppearance` lives in `Forms`: DA is defined in the forms chapter, and its font names resolve against the form's default resources even for FreeText.
- It is a pure model: data only, nothing resolved except an ExtGState (see below).
- Typed, nullable values for what the operators set: font name and size (size 0 means auto-size), fill and stroke color, text state, line parameters. Unmodeled operators are kept raw.
  - Nullable means "the DA did not set it". Never fill in defaults (black, size 1): the defaults come from where the fallback renderer starts drawing, not from the model.
  - Colors are kept as color space plus components, never converted.
  - The font is kept as its resource name, never resolved.
- An ExtGState operator (`gs`) inside DA is resolved during parsing and applied onto the default appearance at its position in the string, so later operators override it and earlier ones are overridden by it. `PdfGraphicsStateParameters` (in `Rendering`) gets an `ApplyToDefaultAppearance` method for this; the `Forms` to `Rendering` dependency is accepted.
- Parsing is a separate internal static class, `PdfDefaultAppearanceParser`, also in `Forms`. It reads the DA string with the content stream tokenizer (`PdfParser` in content stream mode, `PdfOperandStack`, switch on the operator), the same way `PdfType3Font` reads glyph metrics. It does not run the rendering interpreter.

Rejected designs, do not revisit:

- **Using `PdfGraphicsState` as the model.** DA sets the same things as the graphics state, but `PdfGraphicsState` is the interpreter's runtime state: it needs a content host and rendering parameters to exist, holds already converted paints, and cannot tell "not set" from "default".
- **Reusing or reworking ExtGState parameters as the DA model.** ExtGState is a dictionary applied by the `gs` operator; DA is an operator list that may itself contain `gs`.

### Font resolution

- Fonts are never resolved in the model. Whoever draws resolves the font name: the fallback renderer against the form's default resources (`DR`), native controls by mapping the name to their own fonts.

### Field inheritance

- Inherited field values are resolved at load, top-down, the same way the page tree resolves its inherited attributes (`PdfPageResources`). Fields keep only their effective values; nothing walks from a child to its parents at use time.
- The chain starts at `PdfInteractiveForm` (document-wide DA and text alignment) and continues through the field tree, which can be any depth (`Kids`). Dotted full names are built in the same pass. Guard against cycles.
- A key present on a node replaces the inherited value whole. DA strings are never merged operator by operator. Example: the form has DA `/Helv 12 Tf 1 0 0 rg` (red) and a field has DA `/TiRo 12 Tf` (no color): the field draws in Times, black.
- FreeText has its own DA and no inheritance chain.

### No style object

- A compiled style type (DA plus border, background, alignment and flags, handed to native controls and fallback renderers) was considered and rejected. After load-time inheritance the field and widget models already hold every effective value; the font is the only unresolved part, and each consumer resolves it differently. A style object would only duplicate the models.
- Fallback renderers and native controls read the field and widget models directly.

### Fallback renderers

- Static classes, one per control type (plus FreeText), that push commands to an `IPdfCommandProcessor` from parameters, like `PdfAnnotationLineEndingRenderer`. The annotation's `RenderFallback` calls them.
- They live in their own subfolder, apart from the annotation and field models.
- Their input is more than DA and V: border style and colors, background color, rotation, captions and icons come from the widget's appearance characteristics (`MK`) and border style (`BS`). A push button's text is its caption, not V.

### Existing widget and field code

- `PdfWidgetAnnotation`, `PdfFormField` and the field classes are about a year old, unused, and are reworked fully. Do not treat their parsing or inheritance as a reference, and do not preserve their API.
- The extended interaction code (keyboard and mouse interaction interfaces, pointer state, key types in `Forms`) is removed.

## 1. Interactive form, default appearance model + FreeText fallback

- `PdfInteractiveForm` with default resources, document-wide DA and text alignment, "need appearances" and signature flags. FreeText needs the default resources to resolve its DA, so this comes first; the field tree waits for step 2.
- `PdfDefaultAppearance` and `PdfDefaultAppearanceParser` as described above.
- `PdfGraphicsStateParameters.ApplyToDefaultAppearance`.
- FreeText reads its DA.
- FreeText fallback renderer: DA plus the text, from Contents or the plain text of the rich text contents extracted with `PdfRichTextContentParser`. FreeText annotations made by Acrobat always carry an AP, so the fallback matters for other producers.

Files: `PdfPixel/PdfDocument.cs`, `PdfPixel/IPdfDocument.cs`, `PdfPixel/Annotations/PdfFreeTextAnnotation.cs`, `PdfPixel/Rendering/PdfGraphicsStateParameters.cs`

## 2. Widgets and fields refactor

- Root fields on `PdfInteractiveForm`; field tree inheritance as described above. Inherited values: field type, field flags, V, default value, DA, text alignment, maximum length.
- Terminal fields and their widgets: a field merged with its widget in one dictionary, and fields with separate widget kids.
- Remove the extended interaction code.
- Check in Acrobat whether a DA on a separate widget kid is honored; if it is, it is one more step at the end of the chain.

Files: `PdfPixel/Forms`, `PdfPixel/Annotations/PdfWidgetAnnotation.cs`, `PdfPixel/Annotations/PdfAnnotation.cs`

## 3. Control types, one at a time

One fallback renderer per control type: text field, check box, radio button, push button, choice. Check box is a candidate for full support without the native overlay.

## Known, later

Added one at a time, each as its own small step:

- Text alignment (`Q`)
- Border style and color, background color (appearance characteristics, `MK`; border style, `BS`)
- Rotation of the widget content
- Text field flags: multiline, comb, password
- Push-button captions and icons, including rollover and down
- Highlighting mode (`H`)
- Default style string (`DS`): often holds the real font, size and color of Acrobat-made FreeText
- Native control overlay
