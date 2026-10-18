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
- Storage and scheduling are separate:
  - `PdfPanelDocument` (`ContentProvider/`) is the storage of a page collection: the document and its lock, one `PdfPageCacheEntry` per page (everything per page the panel needs), the read accessors and freshness checks, the optional content states the extracted content was produced with, and the notifications (`PageUpdated`, `PageTextExtracted`, `PageThumbnailRendered`, `ExtractedContentCleared`), posted once to the synchronization context given to `PdfPanelPageCollection.FromDocument`.
  - `PdfPanelScheduler` (`Scheduling/`) only processes requests (content, thumbnails, text extraction) and writes results into `PdfPanelDocument` through work items on the queue.
  - Consumers read from `PdfPanelDocument`; only `PdfPanelContext`, `PdfPanelRenderer` and `PdfPanelThumbnails` talk to the scheduler.
- `PdfPanelContext` is created per page collection. It is the single entry point for all viewport/layout updates: it builds `PagesDrawingRequest` and submits to `PdfPanelRenderer`, and in `Render` compares the request's optional content states with the stored ones, clearing extracted content when they differ.
- `PdfPanelRenderer` renders immediately from `PdfPanelDocument`, then triggers background decode via `PdfPanelScheduler.UpdateContent`. When a page is ready, `PdfPanelDocument.PageUpdated` fires on the UI thread and re-draws only that page.
- `SkCanvasExtensions.DrawPage` takes a required `PageDrawFlags` parameter. Full render passes `PageDrawFlags.AllContent`. Partial content updates pass `PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content` — never redraw shadows on partial updates.
