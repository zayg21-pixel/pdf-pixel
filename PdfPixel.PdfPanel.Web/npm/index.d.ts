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
    /** Distance from a character within which the pointer counts as being over it, in unscaled page space. */
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
    /** Handle of the layer configuration the content is rendered with. */
    layerConfiguration: number | null;
    /** Handles of the visible layers. */
    visibleLayers: number[];
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

/** Static info of a page, the shape of the frame page `info`. */
export interface PdfPixelPageInfo {
    label: string;
    /** Crop box in PDF user space. */
    cropBox: PdfPixelRectangle;
    /** Page rotation from the document. */
    rotation: number;
}

/** How the document is displayed when opened (`/PageMode`). */
export type PdfPixelPageMode = 'Unknown' | 'UseNone' | 'UseOutlines' | 'UseThumbs' | 'FullScreen' | 'UseOC' | 'UseAttachments';

/** Page layout used when the document is opened (`/PageLayout`). */
export type PdfPixelPageLayout =
    'Unknown' | 'SinglePage' | 'OneColumn' | 'TwoColumnLeft' | 'TwoColumnRight' | 'TwoPageLeft' | 'TwoPageRight';

/** Document information dictionary. */
export interface PdfPixelDocumentInformation {
    title: string | null;
    author: string | null;
    subject: string | null;
    keywords: string | null;
    creator: string | null;
    producer: string | null;
    /** ISO 8601 creation date. */
    creationDate: string | null;
    /** ISO 8601 modification date. */
    modificationDate: string | null;
    isTrapped: boolean | null;
    /** Entries the document defines beyond the standard ones, keyed by name. */
    customEntries: Record<string, string>;
}

/** User access permissions of an encrypted document. */
export interface PdfPixelPermissions {
    print: boolean;
    modify: boolean;
    copy: boolean;
    annotate: boolean;
    fillForms: boolean;
    assemble: boolean;
    highQualityPrint: boolean;
}

/** Outline item (bookmark). */
export interface PdfPixelOutlineItem {
    /** Handle for `activateOutlineItem`. */
    handle: number;
    title: string | null;
    isBold: boolean;
    isItalic: boolean;
    color: PdfPixelColor | null;
    /** Whether the document opens the item. */
    isExpanded: boolean;
    children: PdfPixelOutlineItem[];
}

/** Node of a layer tree: a switchable layer, or a list of layers with an optional label. */
export interface PdfPixelLayer {
    /** Handle for `setLayerVisible`, reported in `visibleLayers` of the view state. */
    handle: number;
    name: string | null;
    isLayer: boolean;
    /** Whether the document locks the layer against changes through a user interface. */
    isLocked: boolean;
    children: PdfPixelLayer[];
}

/** Layer configuration with its tree of layers. */
export interface PdfPixelLayerConfiguration {
    /** Handle for `setLayerConfiguration`, reported as `layerConfiguration` of the view state. */
    handle: number;
    name: string | null;
    isDefault: boolean;
    layers: PdfPixelLayer[];
}

/** File the document references. */
export interface PdfPixelFile {
    /** Handle for `getFileBytes`, or `null` when the file is not embedded in the document. */
    handle: number | null;
    name: string | null;
    description: string | null;
}

/** File attached to the document or to one of its pages. */
export interface PdfPixelAttachment {
    file: PdfPixelFile;
    /** 1-based number of the page the file is attached to, or `null` when it is attached to the document. */
    pageNumber: number | null;
}

/** Destination in another document: a page with its view, or a named destination. */
export type PdfPixelRemoteDestination = PdfPixelDestination | { name: string };

/** Action the panel does not perform itself. */
export type PdfPixelAction =
    | { type: 'uri'; uri: string | null }
    | { type: 'launch'; file: PdfPixelFile | null; newWindow: boolean | null }
    | { type: 'goToRemote'; file: PdfPixelFile | null; destination: PdfPixelRemoteDestination | null; newWindow: boolean | null }
    | { type: 'named'; name: string | null }
    /** Any other action, with its PDF name (`/S`), such as `GoToE` or `JavaScript`. */
    | { type: 'unsupported'; actionType: string };

/** What triggered a requested action. */
export type PdfPixelActionSource =
    'documentOpen' | 'pageOpen' | 'pageClose' | 'linkAnnotationClick' | 'fileAttachmentAnnotationClick' | 'outline';

/** Request passed to the `setOnActionRequested` callback. */
export interface PdfPixelActionRequest {
    source: PdfPixelActionSource;
    action: PdfPixelAction;
}

/** Document data of a loaded document. */
export interface PdfPixelDocument {
    ok: true;
    pageCount: number;
    /** Static info per page, in page order. */
    pages: PdfPixelPageInfo[];
    version: string | null;
    pageMode: PdfPixelPageMode | null;
    pageLayout: PdfPixelPageLayout | null;
    language: string | null;
    information: PdfPixelDocumentInformation | null;
    /** `null` when the document is not encrypted. */
    permissions: PdfPixelPermissions | null;
    outlines: PdfPixelOutlineItem[];
    layerConfigurations: PdfPixelLayerConfiguration[];
    attachments: PdfPixelAttachment[];
}

/** Update passed to the `setOnThumbnailUpdated` callback. */
export interface PdfPixelThumbnailUpdate {
    /** 1-based page number. */
    pageNumber: number;
    png: Uint8Array;
}

/** Request passed to the `setOnCredentialRequested` callback. */
export interface PdfPixelCredentialRequest {
    /** `required` for the first request, `rejected` after a credential that is not accepted. */
    reason: 'required' | 'rejected';
    /** Access that requires the credential. */
    authEvent: 'documentOpen' | 'embeddedFileOpen';
}

/** Credential that opens an encrypted document. */
export type PdfPixelCredential =
    /** User or owner password of the Standard security handler. */
    | { type: 'password'; password: string };

/** Result of `setDocument`. */
export type PdfPixelDocumentResult =
    | PdfPixelDocument
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
export function setDocument(id: string, documentData: Uint8Array): Promise<PdfPixelDocumentResult>;
export function setOnCredentialRequested(
    id: string,
    callback: (request: PdfPixelCredentialRequest) => PdfPixelCredential | null | Promise<PdfPixelCredential | null>): boolean;
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
export function activateOutlineItem(id: string, handle: number): boolean;
export function setLayerConfiguration(id: string, handle: number): boolean;
export function setLayerVisible(id: string, handle: number, visible: boolean): boolean;
export function getFileBytes(id: string, handle: number): Promise<Uint8Array | null>;
export function setOnActionRequested(id: string, callback: (request: PdfPixelActionRequest) => void): boolean;
export function requestThumbnail(id: string, pageNumber: number): boolean;
export function cancelThumbnail(id: string, pageNumber: number): boolean;
export function setOnThumbnailUpdated(id: string, callback: (update: PdfPixelThumbnailUpdate) => void): boolean;
