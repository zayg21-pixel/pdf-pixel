# PDF Pixel

PDF viewer panel for the browser. PDF Pixel is a native C# PDF rendering library built around SkiaSharp; this package runs it in the browser compiled to WebAssembly, rendering with WebGL or on the CPU. Documents are parsed and rendered entirely on the client — no server-side processing and no dependency on external PDF frameworks.

[Live demo](https://zayg21-pixel.github.io/pdf-pixel/)

## Install

```bash
npm install pdf-pixel
```

Pre-releases are published under the `next` tag:

```bash
npm install pdf-pixel@next
```

## Quick start

```html
<div id="viewer" style="position: relative; width: 800px; height: 600px;"></div>

<script type="module">
    import * as pdfPixel from './node_modules/pdf-pixel/index.js';

    await pdfPixel.initialize();

    const container = document.getElementById('viewer');
    pdfPixel.registerPanel(container.id, container);

    const response = await fetch('document.pdf');
    const result = await pdfPixel.setDocument(container.id, new Uint8Array(await response.arrayBuffer()));
    pdfPixel.requestRedraw(container.id);
</script>
```

The panel id must be the id of its container element. The panel adds its canvases and scroll host to the container.

## Hosting

`index.js`, `canvasInterop.js` and the `_framework/` folder must be served together as static files, with `_framework/` next to `index.js`. The runtime loads `_framework/` relative to its own URL, which bundlers do not follow — copy the package files to a static folder rather than bundling them.

- Serve `.wasm` files with the `application/wasm` MIME type.
- Enable brotli or gzip compression for `.wasm` and `.js` on the server or CDN; the WebAssembly code compresses to roughly a quarter of its size.
- `_framework/` also contains a gzip-compressed copy (`.gz`) of every file. Hosts that cannot compress responses, such as plain blob storage, can load those copies with `loadGzipResource` (see below).

## Custom runtime

`initialize()` creates the .NET runtime with its default configuration. To configure the runtime yourself — for example to load `_framework/` files from another location — create it with the exported `dotnet` builder and pass it to `initialize`:

```js
const runtime = await pdfPixel.dotnet
    .withResourceLoader((type, name, defaultUri) => `https://cdn.example.com/pdf-pixel/_framework/${name}`)
    .create();

await pdfPixel.initialize(runtime);
```

The runtime must be created with the `dotnet` builder of this package. The builder is the .NET WebAssembly host builder; its options may change between .NET versions.

To load the gzip-compressed runtime files and decompress them in the browser, pass `loadGzipResource` as the resource loader:

```js
const runtime = await pdfPixel.dotnet.withResourceLoader(pdfPixel.loadGzipResource).create();
await pdfPixel.initialize(runtime);
```

JavaScript modules, and every file in browsers without `DecompressionStream`, load uncompressed.

The panel logs warnings and errors to the browser console.

## Configuration

`registerPanel` takes an optional configuration:

```js
pdfPixel.registerPanel(container.id, container, {
    useWebGL: true,
    scrollStep: 20,
    yieldInterval: 16,
    minScale: 0.1,
    maxScale: 10,
    zoomStep: 0.1,
    pageGap: 10,
    layoutPadding: { left: 10, top: 10, right: 10, bottom: 10 },
    panelBackgroundColor: '#D3D3D3',
    pageCornerRadius: 0,
    showPageLoadingAnimation: true,
    extractAllText: false,
    textSelectionColor: '#3264DC50',
    searchMatchCase: false,
    searchMatchWholeWord: false,
    searchMatchDiacritics: false,
    searchMatchColor: '#FFC80064',
    searchCurrentMatchColor: '#FF78008C',
    contentSelectionBorderThickness: 2,
    contentSelectionBorderColor: '#000000',
    allowTextSelection: true,
    allowPageContentHover: true,
    allowImageSelection: true,
    showSearchHighlights: true
});
```

Every key is optional; keys that are not set keep their defaults. Colors are hex strings, `#RRGGBB` or `#RRGGBBAA`.

| Key | Description |
| --- | --- |
| `useWebGL` | Render with WebGL instead of the CPU. Initial configuration only. |
| `yieldInterval` | Minimum time in milliseconds between two yields of page decoding to the browser. Initial configuration only. |
| `scrollStep` | Pixels scrolled per mouse wheel line. |
| `minScale`, `maxScale` | Zoom limits. |
| `zoomStep` | Proportional scale change of one zoom step. |
| `pageGap` | Gap between pages, in unscaled page space. |
| `layoutPadding` | Padding around the pages, in device pixels. |
| `panelBackgroundColor` | Background color of the panel behind the pages. |
| `pageCornerRadius` | Corner radius of the pages, in unscaled page space. |
| `showPageLoadingAnimation` | Animate pages that are not decoded yet. |
| `extractAllText` | Extract the text of every page, not only of the rendered pages. Search covers every page only while this is set. |
| `textSelectionColor` | Color of the text selection. |
| `searchMatchCase`, `searchMatchWholeWord`, `searchMatchDiacritics` | Match options of the text search. |
| `searchMatchColor`, `searchCurrentMatchColor` | Highlight colors of the search matches. |
| `contentSelectionBorderThickness`, `contentSelectionBorderColor` | Border of the selected image; thickness in device pixels. |
| `allowTextSelection` | Whether text can be selected with the pointer. |
| `allowPageContentHover` | Whether the page content under the pointer is tracked. |
| `allowImageSelection` | Whether images can be selected with the pointer. |
| `showSearchHighlights` | Whether the search matches are highlighted. |

`updateConfiguration(id, configuration)` changes the configuration of a registered panel. Only the keys that are set change.

## API

| Function | Description |
| --- | --- |
| `initialize(runtime?)` | Starts the .NET runtime, or uses `runtime` created with `dotnet`. Call once before any other function; later calls return the same promise. |
| `dotnet` | The .NET runtime builder, for a custom runtime configuration. |
| `registerPanel(id, container, configuration?)` | Creates a panel in the container element with the id `id`. |
| `unregisterPanel(id)` | Disposes the panel. |
| `loadGzipResource` | Resource loader for `dotnet.withResourceLoader` that loads the gzip-compressed runtime files. |
| `setDocument(id, bytes)` | Loads a PDF document from a `Uint8Array` and resolves to `ok: true` with the document data (see below) or `{ ok: false, reason, message }`, with `reason` one of `empty`, `encrypted`, `unsupported`, `invalid` and `error`. The shown document is kept when loading fails. |
| `setOnCredentialRequested(id, callback)` | Calls `callback({ reason, authEvent })` when an encrypted document needs a credential, on open (`documentOpen`) or when an embedded file is read (`embeddedFileOpen`): `reason` is `required` for the first request and `rejected` after a credential that is not accepted. The callback returns the credential, `{ type: 'password', password }`, or `null` to stop loading, directly or as a promise. Without a callback, an encrypted document fails to load with `encrypted`. |
| `activateOutlineItem(id, handle)` | Performs the action of an outline item, or navigates to its destination when it has none. |
| `setLayerConfiguration(id, handle)` | Renders the content with a layer configuration. |
| `setLayerVisible(id, handle, visible)` | Shows or hides a layer; showing it hides the other layers of its radio-button sets. |
| `getFileBytes(id, handle)` | Resolves to the content of an embedded file, from `attachments` or from a requested action, as a `Uint8Array`, or `null`. An encrypted file requests its credential through `setOnCredentialRequested` with `authEvent` `embeddedFileOpen`. |
| `setOnActionRequested(id, callback)` | Calls `callback({ source, action })` for each action the panel does not perform itself, after the render during which it was requested (see below). |
| `getThumbnailLayout(id)` | Returns `{ pageNumber, width, height, rotation }` per page: the thumbnail size in pixels before rotation, and the page rotation. Call it again after rotating pages or changing the thumbnail configuration. |
| `getThumbnail(id, pageNumber)` | Returns the PNG of a thumbnail that is already produced, or `null`. |
| `setThumbnailVisible(id, pageNumber, visible)` | Sets whether the thumbnail of a page is shown. A visible thumbnail is kept and its PNG is passed to the `setOnThumbnailUpdated` callback when it is ready, at once when it is already produced, and again whenever it is produced anew. A thumbnail that is no longer visible may be evicted; drop your copy of it. |
| `setOnThumbnailUpdated(id, callback)` | Calls `callback({ pageNumber, png })` for each visible thumbnail that is ready. |
| `requestRedraw(id)` | Renders the panel. |
| `setPage(id, pageNumber)` | Scrolls to a 1-based page. |
| `setScale(id, scale)` | Zooms to a scale (`1.0` = 100%) around the panel center. |
| `setAutoScaleMode(id, mode)` | Fits the pages to the panel: `scaleToWidth`, `scaleToHeight` or `noAutoScale`. The mode applies to documents loaded afterwards and returns to `noAutoScale` when the user zooms. |
| `scrollToDestination(id, destination)` | Scrolls to `{ pageNumber, fitType, left?, top?, right?, bottom?, zoom? }` in the page's PDF user space, with a fit type of ISO 32000 Table 149 (`XYZ`, `Fit`, `FitH`, `FitV`, `FitR`, `FitB`, `FitBH`, `FitBV`). Works for pages not shown yet. |
| `rotatePage(id, pageNumber, degrees)`, `rotatePages(id, degrees)` | Rotates one page or every page by a multiple of 90 degrees, added to its current rotation, keeping the current page in view. |
| `setSearchQuery(id, query)` | Searches the document text, or stops searching with `null`. |
| `setCurrentSearchResult(id, result)` | Highlights a result from `searchResults` as current and scrolls to it. |
| `nextSearchResult(id)`, `previousSearchResult(id)` | Selects the next or previous result, wrapping around; with no current result, selects the first result on or after the current page. |
| `updateConfiguration(id, configuration)` | Changes the configuration keys that are set. |
| `setOnStateChanged(id, callback)` | Calls `callback(state)` after every render. |
| `setOnFramePresented(id, callback)` | Calls `callback({ id, canvas, frame })` after every presented frame, with an overlay canvas for custom drawing above the pages. |

The document data returned by `setDocument` includes:

| Field | Description |
| --- | --- |
| `pageCount` | Number of pages. |
| `pages` | Per page in page order, `{ label, width, height, rotation }`, the shape of the frame page `info`. |
| `version` | Version the document conforms to, such as `1.7`, or `null`. |
| `pageMode`, `pageLayout` | How the document is displayed and laid out when opened, as PDF names (`UseOutlines`, `TwoColumnLeft`, …), `Unknown` for an unrecognized value, or `null`. |
| `language` | Natural language of the document, or `null`. |
| `information` | `{ title, author, subject, keywords, creator, producer, creationDate, modificationDate, isTrapped, customEntries }`, the dates in ISO 8601, or `null`. |
| `permissions` | `{ print, modify, copy, annotate, fillForms, assemble, highQualityPrint }`, or `null` when the document is not encrypted. |
| `outlines` | Outline tree, `{ handle, title, isBold, isItalic, color, isExpanded, children }`. |
| `layerConfigurations` | Layer configurations, `{ handle, name, isDefault, layers }`, each layer `{ handle, name, isLayer, isLocked, children }`. |
| `attachments` | Attached files, `{ file, pageNumber }`, `pageNumber` being `null` for a file attached to the document. |

A file is `{ handle, name, description }`, its `handle` being `null` when the file is not embedded. A handle identifies an item for the functions that take one, for the loaded document only.

The panel navigates, switches layers and hides annotations itself. Any other action is passed to the `setOnActionRequested` callback as `{ source, action }`; the panel does not open links or files on its own.

| `source` | Triggered by |
| --- | --- |
| `documentOpen`, `pageOpen`, `pageClose` | Opening the document, a page becoming or leaving the current page. |
| `linkAnnotationClick`, `fileAttachmentAnnotationClick` | A click on a link or file attachment annotation. |
| `outline` | `activateOutlineItem`. |

| `action.type` | Fields |
| --- | --- |
| `uri` | `uri`. |
| `launch` | `file`, `newWindow`. |
| `goToRemote` | `file`, `newWindow` and `destination` in the other document: `{ pageNumber, fitType, … }` as for `scrollToDestination`, `{ name }`, or `null`. |
| `named` | `name` of a named action the panel does not know. |
| `unsupported` | `actionType`, the PDF name of any other action, such as `GoToE` or `JavaScript`. |

The state passed to `setOnStateChanged` includes:

| Field | Description |
| --- | --- |
| `currentPage` | Page shown in the panel center. |
| `scale` | Current zoom scale. |
| `autoScaleMode` | Current automatic scaling mode. |
| `cursorStyle` | Cursor the panel sets on its scroll host, or `null` when it leaves the cursor to the host page. |
| `searchResults` | Results of the search query, `{ pageNumber, startIndex, length, bounds }`, ordered by page. |
| `currentSearchResult` | Result highlighted as current, or `null`. |
| `isTextExtracted` | Whether the text of every page has been extracted, so `searchResults` is final. |
| `annotationPopup` | Messages of the annotation under the pointer, or `null`. |
| `activeContentName` | Alternate description (`/Alt`) of the page content under the pointer, or `null`. |
| `layerConfiguration` | Handle of the layer configuration the content is rendered with, or `null`. |
| `visibleLayers` | Handles of the visible layers. |

## Features

See the [feature support list](https://github.com/zayg21-pixel/pdf-pixel#feature-support) of PDF Pixel.

## License

MIT. Bundled third-party resources keep their own licenses; their notices are in the `licenses/` folder of this package.
