# PdfPixel — Claude Code Guide

## Formatting

The project uses Roslynator with rules enforced as warnings. The linter rewrites code after every edit. Write code that matches these rules so the linter has nothing to change.

### Always-braces
Every `if`, `else`, `for`, `foreach`, `while` body needs braces — even single-line.
```csharp
// wrong
if (x == null) return;

// right
if (x == null)
{
    return;
}
```

### Expression bodies
Use `=>` for simple single-expression members. Use block body when the body spans multiple lines.
```csharp
public int Count => _items.Length;           // expression body ✓
public void Dispose() => _surface?.Dispose(); // expression body ✓
```

### var
Use `var` only when the type is obvious from the right-hand side.
```csharp
var surface = SKSurface.Create(info); // obvious ✓
SKSurface surface = GetSurface();     // not obvious, explicit type ✓
```

### Object creation
Omit the type when it's obvious from context (`new()` not `new SKPaint()`).
```csharp
SKPaint paint = new();       // ✓
using SKPaint paint = new(); // ✓
```

### Flags enum
Use bitwise operator, not `HasFlag`:
```csharp
if ((flags & PageDrawFlags.Shadow) != 0) // ✓
if (flags.HasFlag(PageDrawFlags.Shadow)) // ✗
```

Declare flag values with shift operators:
```csharp
Shadow     = 1 << 0,
Background = 1 << 1,
Content    = 1 << 2,
```

### Null checks
Use equality operator, not pattern matching:
```csharp
if (x == null) // ✓
if (x is null) // ✗
```

### Null-forgiving operator
Never use `!` (null-forgiving). Handle nullability explicitly with null checks, conditional access, or code restructuring.

### Nullable types — no fake fallbacks
When a value may be absent, use the nullable type (`SKRect?`, `int?`, etc.). Never substitute a sentinel value (`SKRect.Empty`, `0`, `string.Empty`) to represent "not present" — that destroys the distinction between absent and explicitly set to empty/zero.

### C# naming
Never use short or abbreviated variable names (`el`, `d`, `p`, `s`). Names must be descriptive.

### Wrapping existing APIs
Never wrap an existing framework method in a helper that only adds noise. Use the framework API directly.

### No task-tracking comments in code
Never add comments that reference the current task, what was changed, or instructions like "already updated, do not modify", "added for X", "do not touch". These belong in commit messages, not in code.

### Object initializers in arguments
Never inline object initializers (e.g. `new SKPaint { ... }`) directly inside method call arguments. Always assign to a named variable first — types like `SKPaint` have no constructor arguments so inlining makes the code unreadable.

### String empty
```csharp
string.Empty // ✓
""           // ✗
```

### Private fields
Always prefix with `_`:
```csharp
private readonly ILogger _logger;
```

### Delegates
Prefer method groups over lambdas:
```csharp
_syncContext.Post(OnPageUpdatedSync, null); // ✓ (when signature matches)
```

### Binary / arrow operators — newline before
```csharp
bool result = a
    && b
    && c;
```

### TODO comments
Never remove a `// TODO` comment when moving, copying, or editing code. TODOs are tracked future work — treat them as load-bearing. The only time a TODO may be removed is when the described work has actually been completed.

### Documentation comments
Multi-line summary style. Use `/// <inheritdoc />` for interface/override implementations — don't repeat the contract.

## Architecture notes

- Rendering always on the UI thread. Decoding always through the work queue (`IWorkQueue`). Panel data may hold live model objects (`PdfImage`, annotations) — the old "plain data only across threads" rule is obsolete.
- `PdfPanelDocument` (root namespace, created by `PdfPanelDocument.FromDocument`) is the panel's document: the `IPdfDocument` and its `DocumentLocker`, the `Pages`, the read accessors and freshness checks, the optional content states the extracted content was produced with, and the notifications (`PageUpdated`, `PageTextExtracted`, `PageThumbnailRendered`, `ExtractedContentCleared`), posted once to the synchronization context given to `FromDocument`. It creates and disposes the `PdfPanelScheduler`.
- `PdfPanelPage` is one page: label, size, rotation and offset, plus the `IPdfPage` (`PdfPage`) and the `PdfPageCacheEntry` holding everything produced for it — the same model as a PDF page owning its resources. There is no separate page-info type.
  - `Rotation` is editable (multiple of 90, normalized to 0-359) and starts from the PDF's rotation; there is no separate user rotation. `RequestPageInfo` snapshots it per request.
  - Page space is top-left, unrotated, with `UserUnit` folded in: `Size` is CropBox × UserUnit and every recording is made with `ApplyPageTransformations(CropBox, scale: UserUnit)`. Nothing else in the panel multiplies by `UserUnit`; only PDF↔page conversions read it from `PdfPage`.
  - MVVM views learn about rotation changes through `PdfThumbnailsViewModel.Synchronize()`, called next to `Layers?.Synchronize()` in WPF. Web does not report rotation: the user reads `getThumbnailLayout` (thumbnail size and rotation per page) after `setDocument` and after its own rotation or thumbnail-configuration changes.
- Web thumbnails: `getThumbnail` reads an already produced PNG without a callback; `requestThumbnail` does nothing when the PNG exists, `cancelThumbnail` does nothing when nothing is requested; the thumbnail callback reports only `{ pageNumber, png }` of requested thumbnails.
- Property reads on `IPdfDocument`/`IPdfPage` are thread-safe; method calls and stream access lock `DocumentLocker`.
- `PdfPanelScheduler` (`Scheduling/`) only processes requests (content, thumbnails, text extraction) and writes results into each page's cache entry through work items on the queue; work items take the `PdfPanelPage`. Only `PdfPanelContext`, `PdfPanelRenderer` and `PdfPanelThumbnails` talk to the scheduler.
- `PdfPanelContext` is created per `PdfPanelDocument`. It is the single entry point for all viewport/layout updates: it builds `PagesDrawingRequest` and submits to `PdfPanelRenderer`, and in `Render` compares the request's optional content states with the stored ones, clearing extracted content when they differ.
- `PdfPanelRenderer` renders immediately from `PdfPanelDocument`, then triggers background decode via `PdfPanelScheduler.UpdateContent`. When a page is ready, `PdfPanelDocument.PageUpdated` fires on the UI thread and re-draws only that page.
- `SkCanvasExtensions.DrawPage` takes a required `PageDrawFlags` parameter. Full render passes `PageDrawFlags.AllContent`. Partial content updates pass `PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content` — never redraw shadows on partial updates.
