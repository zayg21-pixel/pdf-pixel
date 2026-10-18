/** Hex color string, `#RRGGBB` or `#RRGGBBAA`. */
export type PdfPixelColor = string;

/** Rectangle with edges in the space its field documents. */
export interface PdfPixelRectangle {
    left: number;
    top: number;
    right: number;
    bottom: number;
}

/** Width and height. */
export interface PdfPixelSize {
    width: number;
    height: number;
}

/** Automatic scaling mode of a panel. */
export type PdfPixelAutoScaleMode = 'noAutoScale' | 'scaleToWidth' | 'scaleToHeight';

/** Pages whose thumbnails are produced without being requested. */
export type PdfPixelThumbnailPreloadMode = 'none' | 'visiblePages' | 'nearestPages';

/** Fit type of a destination (ISO 32000 Table 149). */
export type PdfPixelFitType = 'XYZ' | 'Fit' | 'FitH' | 'FitV' | 'FitR' | 'FitB' | 'FitBH' | 'FitBV';

/** Panel configuration; every key is optional and keys that are not set keep their current values. */
export interface PdfPixelConfiguration {
    /** Render with WebGL instead of the CPU. Initial configuration only. */
    useWebGL?: boolean;
    /** Minimum time in milliseconds between two yields of page decoding to the browser. Initial configuration only. */
    yieldInterval?: number;
    /** Pixels scrolled per mouse wheel line. */
    scrollStep?: number;
    minScale?: number;
    maxScale?: number;
    zoomStep?: number;
    /** Gap between pages, in unscaled page space. */
    pageGap?: number;
    /** Padding around the pages, in device pixels. */
    layoutPadding?: Partial<PdfPixelRectangle>;
    panelBackgroundColor?: PdfPixelColor;
    /** Corner radius of the pages, in unscaled page space. */
    pageCornerRadius?: number;
    showPageLoadingAnimation?: boolean;
    /** Extract the text of every page, not only of the rendered pages. */
    extractAllText?: boolean;
    textSelectionColor?: PdfPixelColor;
    searchMatchCase?: boolean;
    searchMatchWholeWord?: boolean;
    searchMatchDiacritics?: boolean;
    searchMatchColor?: PdfPixelColor;
    searchCurrentMatchColor?: PdfPixelColor;
    /** Width of the border of the selected image, in device pixels. */
    contentSelectionBorderThickness?: number;
    contentSelectionBorderColor?: PdfPixelColor;
    allowTextSelection?: boolean;
    allowPageContentHover?: boolean;
    allowImageSelection?: boolean;
    showSearchHighlights?: boolean;
    /** Use the page's embedded thumbnail image as its thumbnail when present. */
    usePageThumbnails?: boolean;
    /** Size in pixels a rendered page thumbnail is fitted within, keeping its aspect ratio. */
    thumbnailRenderMaximumSize?: Partial<PdfPixelSize>;
    /** Maximum number of page thumbnails kept, the ones nearest to the first visible page. */
    thumbnailMaximumCount?: number;
    thumbnailPreloadMode?: PdfPixelThumbnailPreloadMode;
    /** Draw the thumbnail of a page that has no decoded content yet in place of the page. */
    useThumbnailsAsPlaceholders?: boolean;
    antialias?: boolean;
    /** Snap rectangles and image tiles to whole device pixels. */
    snapToDevicePixels?: boolean;
    /** Edge length of a single content tile, in device pixels. */
    tileSize?: number;
    animationFps?: number;
    /** Time in milliseconds a scrolled view waits before page content decoding starts. */
    scrollContentUpdateDelay?: number;
    /** Time in milliseconds a zoomed view waits before page content decoding starts. */
    zoomContentUpdateDelay?: number;
    /** Number of pages ahead of the visible pages in the scroll direction whose content is decoded after them; 0 decodes no pages ahead. */
    lookAheadPageCount?: number;
    /** Number of pages besides the visible ones whose decoded content is kept; 0 keeps only the visible pages. */
    contentCacheSize?: number;    /** Distance from a character within which the pointer counts as being over it, in unscaled page space. */
    textCharacterHitRadius?: number;
    /** Vertical distance between two characters, as a fraction of character height, within which they highlight as one strip. */
    textLineMergeThreshold?: number;
}

/** Result of a text search: a run of characters on a page. */
export interface PdfPixelSearchResult {
    /** 1-based page number. */
    pageNumber: number;
    /** Index of the first matched character on the page. */
    startIndex: number;
    /** Number of matched characters. */
    length: number;
    /** Area the matched characters cover, in unscaled page space. */
    bounds: PdfPixelRectangle;
}

/** Message of an annotation popup, with its replies. */
export interface PdfPixelAnnotationMessage {
    title: string | null;
    contents: string | null;
    /** ISO 8601 creation date. */
    creationDate: string | null;
    replies: PdfPixelAnnotationMessage[];
}

/** Popup of the annotation under the pointer. */
export interface PdfPixelAnnotationPopup {
    isInteractive: boolean;
    messages: PdfPixelAnnotationMessage[];
}

/** View state passed to the `setOnStateChanged` callback after every render. */
export interface PdfPixelState {
    /** Page shown in the panel center. */
    currentPage: number;
    pageCount: number;
    scale: number;
    autoScaleMode: PdfPixelAutoScaleMode;
    verticalOffset: number;
    horizontalOffset: number;
    scrollWidth: number;
    scrollHeight: number;
    /** Cursor the panel requests, or `null` when it leaves the cursor to the host. */
    cursorStyle: string | null;
    searchQuery: string | null;
    searchResults: PdfPixelSearchResult[];
    currentSearchResult: PdfPixelSearchResult | null;
    /** Whether the text of every page has been extracted, so `searchResults` is final. */
    isTextExtracted: boolean;
    annotationPopup: PdfPixelAnnotationPopup | null;
    /** Alternate description (`/Alt`) of the page content under the pointer. */
    activeContentName: string | null;
    mouseX: number | null;
    mouseY: number | null;
    pointerPressed: boolean;
    panelWidth: number;
    panelHeight: number;
    containerWidth: number;
    containerHeight: number;
    devicePixelScale: number;
}

/** Visible page of a presented frame. */
export interface PdfPixelFramePage {
    /** 1-based page number. */
    pageNumber: number;
    info: {
        label: string;
        /** Crop box in PDF user space. */
        cropBox: PdfPixelRectangle;
        /** Page rotation from the document. */
        rotation: number;
    };
    /** Rotation applied by the user. */
    userRotation: number;
    /** Page size after rotation. */
    rotatedSize: PdfPixelSize;
    /**
     * Returns the matrix that maps panel pixels to page space rotated by `rotation` degrees,
     * with the origin at the top-left corner of the rotated page.
     */
    getPanelToPage(rotation: number): DOMMatrix;
}

/** Frame passed to the `setOnFramePresented` callback. */
export interface PdfPixelFrame {
    /** Panel size in device pixels. */
    panelSize: PdfPixelSize;
    hostToPanel: DOMMatrix;
    pages: PdfPixelFramePage[];
}

/** Destination of `scrollToDestination`, in the page's PDF user space. */
export interface PdfPixelDestination {
    /** 1-based page number. */
    pageNumber: number;
    fitType: PdfPixelFitType;
    left?: number;
    top?: number;
    right?: number;
    bottom?: number;
    /** Zoom of the `XYZ` fit type, `1` = 100%. */
    zoom?: number;
}

/** Result of `setDocument`. */
export type PdfPixelDocumentResult =
    | { ok: true; pageCount: number }
    | { ok: false; reason: 'empty' | 'encrypted' | 'unsupported' | 'invalid' | 'error'; message: string };

/** Runtime created with the `dotnet` builder. */
export interface PdfPixelRuntime {
    setModuleImports: Function;
    getAssemblyExports: Function;
}

/** The .NET runtime builder, for a custom runtime configuration. */
export const dotnet: any;

/** Starts the .NET runtime, or uses `runtime`. Every call returns the promise of the first one. */
export function initialize(runtime?: PdfPixelRuntime): Promise<void>;

/** Resource loader for `dotnet.withResourceLoader` that loads the gzip-compressed runtime resources. */
export function loadGzipResource(type: string, name: string, defaultUri: string): Promise<Response> | undefined;

export function registerPanel(id: string, containerElement: HTMLElement, configuration?: PdfPixelConfiguration): boolean;
export function unregisterPanel(id: string): boolean;
export function setDocument(id: string, documentData: Uint8Array): PdfPixelDocumentResult;
export function requestRedraw(id: string): boolean;
export function setPage(id: string, pageNumber: number): boolean;
export function setScale(id: string, scale: number): boolean;
export function setAutoScaleMode(id: string, mode: PdfPixelAutoScaleMode): boolean;
export function scrollToDestination(id: string, destination: PdfPixelDestination): boolean;
export function setPageRotation(id: string, pageNumber: number, degrees: number): boolean;
export function setRotation(id: string, degrees: number): boolean;
export function setSearchQuery(id: string, query: string | null): boolean;
export function setCurrentSearchResult(id: string, result: PdfPixelSearchResult | null): boolean;
export function nextSearchResult(id: string): boolean;
export function previousSearchResult(id: string): boolean;
export function updateConfiguration(id: string, configuration: PdfPixelConfiguration): boolean;
export function setOnStateChanged(id: string, callback: (state: PdfPixelState) => void): boolean;
export function setOnFramePresented(
    id: string,
    callback: (args: { id: string; canvas: HTMLCanvasElement; frame: PdfPixelFrame }) => void): boolean;
