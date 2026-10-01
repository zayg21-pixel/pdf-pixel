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
    pdfPixel.setDocument(container.id, new Uint8Array(await response.arrayBuffer()));
    pdfPixel.requestRedraw(container.id);
</script>
```

The panel id must be the id of its container element. The panel adds its canvases and scroll host to the container.

## Hosting

`index.js`, `canvasInterop.js` and the `_framework/` folder must be served together as static files, with `_framework/` next to `index.js`. The runtime loads `_framework/` relative to its own URL, which bundlers do not follow — copy the package files to a static folder rather than bundling them.

- Serve `.wasm` files with the `application/wasm` MIME type.
- The package ships uncompressed files. Enable brotli or gzip compression for `.wasm` and `.js` on the server or CDN; the WebAssembly code compresses to roughly a quarter of its size.

## Custom runtime

`initialize()` creates the .NET runtime with its default configuration. To configure the runtime yourself — for example to load `_framework/` files from another location — create it with the exported `dotnet` builder and pass it to `initialize`:

```js
const runtime = await pdfPixel.dotnet
    .withResourceLoader((type, name, defaultUri) => `https://cdn.example.com/pdf-pixel/_framework/${name}`)
    .create();

await pdfPixel.initialize(runtime);
```

The runtime must be created with the `dotnet` builder of this package. The builder is the .NET WebAssembly host builder; its options may change between .NET versions.

The panel logs warnings and errors to the browser console.

## Configuration

`registerPanel` takes an optional configuration:

```js
pdfPixel.registerPanel(container.id, container, {
    useWebGL: true,
    scrollStep: 20,
    yieldInterval: 16,
    settings: {
        minScale: 0.1,
        maxScale: 10,
        zoomStep: 0.1,
        layout: { pageGap: 10, padding: { left: 10, top: 10, right: 10, bottom: 10 } },
        renderer: { backgroundColor: '#D3D3D3', pageCornerRadius: 0, showPageLoadingAnimation: true },
        text: { extractText: false, selectionColor: '#3264DC50' },
        search: { matchCase: false, wholeWord: false, matchDiacritics: false, matchColor: '#FFC80064', currentMatchColor: '#FF78008C' }
    }
});
```

Every key is optional; keys that are not set keep their defaults. Colors are hex strings, `#RRGGBB` or `#RRGGBBAA`.

| Key | Description |
| --- | --- |
| `useWebGL` | Render with WebGL instead of the CPU. Initial configuration only. |
| `yieldInterval` | Minimum time in milliseconds between two yields of page decoding to the browser. Initial configuration only. |
| `scrollStep` | Pixels scrolled per mouse wheel line. |
| `settings.minScale`, `settings.maxScale` | Zoom limits. |
| `settings.zoomStep` | Proportional scale change of one zoom step. |
| `settings.layout` | Gap between pages, in unscaled page space, and padding around them, in device pixels. |
| `settings.renderer` | Background color, page corner radius and the loading animation of pages not decoded yet. |
| `settings.text.extractText` | Extract the text of every page, not only of the rendered pages. Search covers every page only while this is set. |
| `settings.text.selectionColor` | Color of the text selection. |
| `settings.search` | Match options and highlight colors of the text search. |

`updateConfiguration(id, configuration)` changes the configuration of a registered panel. Only the keys that are set change.

## API

| Function | Description |
| --- | --- |
| `initialize(runtime?)` | Starts the .NET runtime, or uses `runtime` created with `dotnet`. Call once before any other function; later calls return the same promise. |
| `dotnet` | The .NET runtime builder, for a custom runtime configuration. |
| `registerPanel(id, container, configuration?)` | Creates a panel in the container element with the id `id`. |
| `unregisterPanel(id)` | Disposes the panel. |
| `setDocument(id, bytes)` | Loads a PDF document from a `Uint8Array`. |
| `requestRedraw(id)` | Renders the panel. |
| `setPage(id, pageNumber)` | Scrolls to a 1-based page. |
| `setScale(id, scale)` | Zooms to a scale (`1.0` = 100%) around the panel center. |
| `setSearchQuery(id, query)` | Searches the document text, or stops searching with `null`. |
| `setCurrentSearchResult(id, result)` | Highlights a result from `searchResults` as current and scrolls to it. |
| `nextSearchResult(id)`, `previousSearchResult(id)` | Selects the next or previous result, wrapping around; with no current result, selects the first result on or after the current page. |
| `updateConfiguration(id, configuration)` | Changes the configuration keys that are set. |
| `setOnStateChanged(id, callback)` | Calls `callback(state)` after every render. |
| `setOnFramePresented(id, callback)` | Calls `callback({ id, canvas, frame })` after every presented frame, with an overlay canvas for custom drawing above the pages. |

The state passed to `setOnStateChanged` includes:

| Field | Description |
| --- | --- |
| `currentPage`, `pageCount` | Page shown in the panel center and number of pages. |
| `scale` | Current zoom scale. |
| `searchResults` | Results of the search query, `{ pageNumber, startIndex, length, bounds }`, ordered by page. |
| `currentSearchResult` | Result highlighted as current, or `null`. |
| `isTextExtracted` | Whether the text of every page has been extracted, so `searchResults` is final. |
| `annotationPopup` | Messages of the annotation under the pointer, or `null`. |

## Features

See the [feature support list](https://github.com/zayg21-pixel/pdf-pixel#feature-support) of PDF Pixel.

## License

MIT. Bundled third-party resources keep their own licenses; their notices are in the `licenses/` folder of this package.
