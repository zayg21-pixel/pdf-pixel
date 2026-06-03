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

- Rendering always on UI thread. Decoding always on background thread. No direct cross-thread references — communicate via plain data (`PageUpdatedArgs`, `UpdateContentRequest`).
- `PdfPanelContext` is the single entry point for all viewport/layout updates. It builds `PagesDrawingRequest` and submits to `PdfPanelRenderer`.
- `PdfPanelRenderer` renders immediately from cache, then triggers background decode via `IPdfPageContentProvider`. When a page is ready, `OnPageUpdated` fires on the UI thread and re-draws only that page.
- `SkCanvasExtensions.DrawPage` takes a required `PageDrawFlags` parameter. Full render passes `PageDrawFlags.All`. Partial content updates pass `PageDrawFlags.Background | PageDrawFlags.Content` — never redraw shadows on partial updates.
