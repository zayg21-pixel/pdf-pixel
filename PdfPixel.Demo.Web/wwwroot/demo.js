import * as pdfPixel from './index.js';

await pdfPixel.initialize();

const container = document.getElementById('canvasContainer');
const containerId = container.id;

await pdfPixel.registerPanel(containerId, container, {
    panelBackgroundColor: '#D3D3D3',
    showPageLoadingAnimation: false
});

pdfPixel.setAutoScaleMode(containerId, 'scaleToHeight');

const loadPdfBtn = document.getElementById('loadPdfBtn');
const pdfFileInput = document.getElementById('pdfFileInput');
const prevPageButton = document.getElementById('prevPage');
const nextPageButton = document.getElementById('nextPage');
const pageInput = document.getElementById('pageInput');
const pageTotalLabel = document.getElementById('pageTotal');
const zoomLevelLabel = document.getElementById('zoomLevel');
const zoomOutButton = document.getElementById('zoomOut');
const zoomInButton = document.getElementById('zoomIn');
const resetZoomButton = document.getElementById('resetZoom');
const autoScaleButton = document.getElementById('autoScale');
const autoScaleIcon = document.getElementById('autoScaleIcon');
const rotateAllPagesButton = document.getElementById('rotateAllPages');
const rotatePageButton = document.getElementById('rotatePage');
const searchToggleButton = document.getElementById('searchToggle');
const pointerPopupEl = document.getElementById('pointerPopup');
const footer = document.getElementById('footer');
const searchInput = document.getElementById('searchInput');
const searchStatus = document.getElementById('searchStatus');
const previousSearchResultButton = document.getElementById('previousSearchResult');
const nextSearchResultButton = document.getElementById('nextSearchResult');
const closeSearchButton = document.getElementById('closeSearch');
const searchMatchCaseInput = document.getElementById('searchMatchCase');
const searchWholeWordInput = document.getElementById('searchWholeWord');
const searchMatchDiacriticsInput = document.getElementById('searchMatchDiacritics');
const sideRail = document.getElementById('sideRail');
const sidePanelEl = document.getElementById('sidePanel');
const sideSplitter = document.getElementById('sideSplitter');
const thumbnailList = document.getElementById('thumbnailList');
const outlineTree = document.getElementById('outlineTree');
const layerConfigurationSelect = document.getElementById('layerConfigurationSelect');
const layerTree = document.getElementById('layerTree');
const attachmentList = document.getElementById('attachmentList');
const passwordDialog = document.getElementById('passwordDialog');
const passwordText = document.getElementById('passwordText');
const passwordInput = document.getElementById('passwordInput');
const passwordError = document.getElementById('passwordError');

const autoScaleIcons = {
    scaleToWidth: 'fit_width',
    scaleToHeight: 'height',
    noAutoScale: 'open_in_full'
};

const thumbnailWidth = 120;
const thumbnailMaximumHeight = 170;
const sidePanelMinimumWidth = 160;
const sidePanelMaximumWidth = 600;

let currentState = { currentPage: 0, scale: 1.0, autoScaleMode: 'scaleToHeight', layerConfiguration: null, visibleLayers: [] };
let documentData = null;
let documentSource = null;
const pendingWindows = new Map();
let searchInputTimer = null;
let isSearchOpen = false;
let sidePanel = null;
let allPagesRotation = 0;
const pageRotations = new Map();

let thumbnailItems = [];
let thumbnailImageUrls = [];
let thumbnailObserver = null;
let currentThumbnailItem = null;

let renderedLayerConfiguration = null;
let layerCheckboxes = new Map();

// ── Document ─────────────────────────────────────────────────

/**
 * Opens a document source `{ bytes, fileName, baseUrl }` at `destination`: in this window when `newWindow` is false,
 * otherwise in a new window. `baseUrl` is the address relative file names of the document resolve against, or null.
 */
function openDocument(source, destination, newWindow) {
    if (newWindow === false) {
        loadDocument(source, destination);
        return;
    }

    const child = window.open(`${location.pathname}#child`, '_blank');
    if (!child) {
        console.warn(`${source.fileName} is not opened: the browser blocked the new window.`);
        return;
    }

    pendingWindows.set(child, { source, destination });
}

async function openDocumentFromUrl(url, destination, newWindow) {
    let response;
    try {
        response = await fetch(url);
    } catch (error) {
        console.warn(`${url} is not loaded: ${error.message}`);
        return;
    }

    if (!response.ok) {
        console.warn(`${url} is not loaded: ${response.status} ${response.statusText}`);
        return;
    }

    const bytes = new Uint8Array(await response.arrayBuffer());
    const fileName = decodeURIComponent(url.pathname.split('/').pop() || url.href);
    openDocument({ bytes, fileName, baseUrl: url.href }, destination, newWindow);
}

async function loadDocument(source, destination) {
    const result = await pdfPixel.setDocument(containerId, source.bytes);
    if (!result.ok) {
        console.error(`The document is not loaded (${result.reason}): ${result.message}`);
        return;
    }

    documentData = result;
    documentSource = source;
    allPagesRotation = 0;
    pageRotations.clear();
    document.title = `${getDocumentTitle(result, source.fileName)} – PDF Pixel`;

    pdfPixel.setAutoScaleMode(containerId, 'scaleToHeight');
    searchToggleButton.disabled = false;

    buildThumbnails(result.pages);
    buildOutlines(result.outlines);
    buildLayerConfigurations(result.layerConfigurations);
    buildAttachments(result.attachments);
    updateSideRail();
    setSidePanel(null);

    if (destination && destination.pageNumber !== undefined) {
        pdfPixel.scrollToDestination(containerId, destination);
    } else if (destination) {
        console.log(`Named destination ${destination.name} is not supported.`);
    }

    pdfPixel.requestRedraw(containerId);
}

function isPdfFileName(fileName) {
    return fileName !== null && fileName.toLowerCase().endsWith('.pdf');
}

/**
 * Opens an embedded PDF file in a new window, or in this window when `newWindow` is false; saves any other file.
 */
async function openEmbeddedFile(file, newWindow) {
    if (!isPdfFileName(file.name)) {
        saveFile(file);
        return;
    }

    const bytes = await pdfPixel.getFileBytes(containerId, file.handle);
    if (!bytes) {
        console.warn(`Embedded file ${file.name} is not read.`);
        return;
    }

    openDocument({ bytes, fileName: file.name, baseUrl: null }, null, newWindow);
}

/**
 * Returns the address of a file the document refers to, relative to the address the document was loaded from,
 * or null when it cannot be resolved.
 */
function resolveFileUrl(file) {
    if (!file || !file.name) {
        console.warn('Referenced file has no name.');
        return null;
    }

    if (!documentSource || !documentSource.baseUrl) {
        console.warn(`${file.name} cannot be resolved: the document is not loaded from an address.`);
        return null;
    }

    return new URL(file.name, documentSource.baseUrl);
}

// ── Password ─────────────────────────────────────────────────

function requestPassword(text, rejected) {
    return new Promise((resolve) => {
        passwordText.textContent = text;
        passwordInput.value = '';
        passwordError.hidden = !rejected;

        passwordDialog.addEventListener('close', () => {
            const accepted = passwordDialog.returnValue === 'open';
            resolve(accepted ? { type: 'password', password: passwordInput.value } : null);
        }, { once: true });

        passwordDialog.returnValue = '';
        passwordDialog.showModal();
        passwordInput.focus();
    });
}

passwordInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        e.preventDefault();
        passwordDialog.close('open');
    }
});

pdfPixel.setOnCredentialRequested(containerId, ({ reason, authEvent }) => {
    const text = (authEvent === 'embeddedFileOpen')
        ? 'The attached files of this document are protected. Enter the document password to open them.'
        : 'This document is protected. Enter its password to open it.';

    return requestPassword(text, reason === 'rejected');
});

// ── New windows ──────────────────────────────────────────────

window.addEventListener('message', (event) => {
    if (event.origin !== location.origin || !event.data) {
        return;
    }

    if (event.data.type === 'pdf-pixel-ready') {
        const pending = pendingWindows.get(event.source);
        if (!pending) {
            return;
        }

        pendingWindows.delete(event.source);
        event.source.postMessage({ type: 'pdf-pixel-document', ...pending }, location.origin);
        return;
    }

    if (event.data.type === 'pdf-pixel-document' && event.source === window.opener) {
        history.replaceState(null, '', location.pathname);
        loadDocument(event.data.source, event.data.destination);
    }
});

function getDocumentTitle(result, fileName) {
    const title = result.information?.title;
    return (title && title.trim().length > 0) ? title : fileName;
}

// ── Search ───────────────────────────────────────────────────

function setSearchOpen(open) {
    isSearchOpen = open;
    footer.hidden = !open;
    searchToggleButton.classList.toggle('active', open);

    if (open) {
        searchInput.focus();
        searchInput.select();
        return;
    }

    clearTimeout(searchInputTimer);
    searchInput.value = '';
    pdfPixel.updateConfiguration(containerId, { extractAllText: false });
    pdfPixel.setCurrentSearchResult(containerId, null);
    pdfPixel.setSearchQuery(containerId, null);
}

function updateSearchStatus(state) {
    const results = state.searchResults;
    const current = state.currentSearchResult;
    const currentIndex = current
        ? results.findIndex(result => result.pageNumber === current.pageNumber
            && result.startIndex === current.startIndex
            && result.length === current.length)
        : -1;

    if (searchInput.value.length === 0) {
        searchStatus.textContent = '';
    } else if (!state.isTextExtracted) {
        searchStatus.textContent = 'Searching...';
    } else if (results.length === 0) {
        searchStatus.textContent = 'Not found';
    } else if (currentIndex >= 0) {
        searchStatus.textContent = `${currentIndex + 1} of ${results.length}`;
    } else {
        searchStatus.textContent = `${results.length} found`;
    }
}

searchToggleButton.addEventListener('click', () => setSearchOpen(!isSearchOpen));
closeSearchButton.addEventListener('click', () => setSearchOpen(false));

document.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f' && documentData) {
        e.preventDefault();
        setSearchOpen(true);
    }
});

searchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        setSearchOpen(false);
    }
});

searchInput.addEventListener('input', () => {
    clearTimeout(searchInputTimer);
    searchInputTimer = setTimeout(() => {
        pdfPixel.updateConfiguration(containerId, { extractAllText: searchInput.value.length > 0 });
        pdfPixel.setCurrentSearchResult(containerId, null);
        pdfPixel.setSearchQuery(containerId, searchInput.value);
    }, 300);
});

previousSearchResultButton.addEventListener('click', () => pdfPixel.previousSearchResult(containerId));
nextSearchResultButton.addEventListener('click', () => pdfPixel.nextSearchResult(containerId));

searchMatchCaseInput.addEventListener('change', () =>
    pdfPixel.updateConfiguration(containerId, { searchMatchCase: searchMatchCaseInput.checked }));
searchWholeWordInput.addEventListener('change', () =>
    pdfPixel.updateConfiguration(containerId, { searchMatchWholeWord: searchWholeWordInput.checked }));
searchMatchDiacriticsInput.addEventListener('change', () =>
    pdfPixel.updateConfiguration(containerId, { searchMatchDiacritics: searchMatchDiacriticsInput.checked }));

// ── Pointer popup ────────────────────────────────────────────

function updatePointerPopup(state) {
    const popup = state.annotationPopup;
    const hasMessages = popup && popup.messages && popup.messages.length > 0;
    const contentName = state.activeContentName;

    if ((!hasMessages && !contentName) || state.mouseX == null || state.mouseY == null) {
        pointerPopupEl.classList.remove('visible');
        return;
    }

    pointerPopupEl.innerHTML = hasMessages
        ? renderAnnotationMessages(popup.messages)
        : `<div class="annotation-popup-content">${escapeHtml(contentName)}</div>`;

    // Position below and to the right of the cursor
    const containerRect = container.getBoundingClientRect();
    const offsetX = 12;
    const offsetY = 16;
    let left = state.mouseX + offsetX;
    let top = state.mouseY + offsetY;

    // Clamp so popup stays within the container
    const maxLeft = containerRect.width - pointerPopupEl.offsetWidth - 4;
    const maxTop = containerRect.height - pointerPopupEl.offsetHeight - 4;
    if (left > maxLeft) {
        left = state.mouseX - pointerPopupEl.offsetWidth - offsetX;
    }
    if (top > maxTop) {
        top = state.mouseY - pointerPopupEl.offsetHeight - offsetY;
    }

    pointerPopupEl.style.left = Math.max(0, left) + 'px';
    pointerPopupEl.style.top = Math.max(0, top) + 'px';
    pointerPopupEl.classList.add('visible');
}

function renderAnnotationMessages(messages) {
    let html = '';
    for (const message of messages) {
        if (message.title) {
            html += `<div class="annotation-popup-title">${escapeHtml(message.title)}</div>`;
        }
        if (message.contents) {
            html += `<div class="annotation-popup-content">${escapeHtml(message.contents)}</div>`;
        }
        if (message.creationDate) {
            const dateText = new Date(message.creationDate).toLocaleString();
            html += `<div class="annotation-popup-date">${dateText}</div>`;
        }
        if (message.replies.length > 0) {
            html += `<div class="annotation-popup-replies">${renderAnnotationMessages(message.replies)}</div>`;
        }
    }
    return html;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ── Side panel ───────────────────────────────────────────────

function setSidePanel(name) {
    sidePanel = name;
    sidePanelEl.hidden = name === null;
    sideSplitter.hidden = name === null;

    for (const section of sidePanelEl.querySelectorAll('.side-section')) {
        section.hidden = section.dataset.sidePanel !== name;
    }

    for (const button of sideRail.querySelectorAll('button')) {
        button.classList.toggle('active', button.dataset.sidePanel === name);
    }
}

function updateSideRail() {
    sideRail.hidden = documentData === null;

    const available = {
        thumbnails: true,
        outlines: documentData.outlines.length > 0,
        layers: documentData.layerConfigurations.length > 0,
        attachments: documentData.attachments.length > 0
    };

    for (const button of sideRail.querySelectorAll('button')) {
        button.hidden = !available[button.dataset.sidePanel];
    }
}

for (const button of sideRail.querySelectorAll('button')) {
    button.addEventListener('click', () => {
        const name = button.dataset.sidePanel;
        setSidePanel(sidePanel === name ? null : name);
    });
}

sideSplitter.addEventListener('pointerdown', (e) => {
    const startX = e.clientX;
    const startWidth = sidePanelEl.offsetWidth;
    sideSplitter.setPointerCapture(e.pointerId);

    const onPointerMove = (moveEvent) => {
        const width = Math.min(sidePanelMaximumWidth, Math.max(sidePanelMinimumWidth, startWidth + moveEvent.clientX - startX));
        sidePanelEl.style.width = width + 'px';
    };

    const onPointerUp = () => {
        sideSplitter.removeEventListener('pointermove', onPointerMove);
        sideSplitter.removeEventListener('pointerup', onPointerUp);
    };

    sideSplitter.addEventListener('pointermove', onPointerMove);
    sideSplitter.addEventListener('pointerup', onPointerUp);
});

// ── Thumbnails ───────────────────────────────────────────────

function buildThumbnails(pages) {
    if (thumbnailObserver) {
        thumbnailObserver.disconnect();
    }

    for (const url of thumbnailImageUrls) {
        if (url) {
            URL.revokeObjectURL(url);
        }
    }

    thumbnailList.replaceChildren();
    thumbnailItems = [];
    thumbnailImageUrls = pages.map(() => null);
    currentThumbnailItem = null;

    thumbnailObserver = new IntersectionObserver(onThumbnailsIntersected, { root: thumbnailList, rootMargin: '200px 0px' });

    pages.forEach((page, pageIndex) => {
        const pageNumber = pageIndex + 1;
        const item = document.createElement('div');
        item.className = 'thumbnail-item';
        item.dataset.pageNumber = String(pageNumber);
        item.addEventListener('click', () => pdfPixel.setPage(containerId, pageNumber));

        const frame = createThumbnailFrame(page);
        const label = document.createElement('div');
        label.className = 'thumbnail-label';
        label.textContent = page.label;

        item.append(frame, label);
        thumbnailList.append(item);
        thumbnailItems.push(item);
        thumbnailObserver.observe(item);
    });
}

function createThumbnailFrame(page) {
    const cropWidth = Math.abs(page.cropBox.right - page.cropBox.left);
    const cropHeight = Math.abs(page.cropBox.top - page.cropBox.bottom);
    const isQuarterTurn = (((page.rotation % 360) + 360) % 180) !== 0;
    const shownWidth = isQuarterTurn ? cropHeight : cropWidth;
    const shownHeight = isQuarterTurn ? cropWidth : cropHeight;
    const scale = Math.min(thumbnailWidth / shownWidth, thumbnailMaximumHeight / shownHeight);

    const frame = document.createElement('div');
    frame.className = 'thumbnail-frame';
    frame.style.width = Math.round(shownWidth * scale) + 'px';
    frame.style.height = Math.round(shownHeight * scale) + 'px';

    const image = document.createElement('img');
    image.alt = '';
    image.hidden = true;
    image.style.width = Math.round(cropWidth * scale) + 'px';
    image.style.height = Math.round(cropHeight * scale) + 'px';
    image.style.transform = `translate(-50%, -50%) rotate(${page.rotation}deg)`;

    frame.append(image);
    return frame;
}

function onThumbnailsIntersected(entries) {
    for (const entry of entries) {
        const pageNumber = Number(entry.target.dataset.pageNumber);
        if (entry.isIntersecting) {
            pdfPixel.requestThumbnail(containerId, pageNumber);
        } else {
            pdfPixel.cancelThumbnail(containerId, pageNumber);
        }
    }
}

pdfPixel.setOnThumbnailUpdated(containerId, ({ pageNumber, png }) => {
    const item = thumbnailItems[pageNumber - 1];
    if (!item) {
        return;
    }

    const previousUrl = thumbnailImageUrls[pageNumber - 1];
    if (previousUrl) {
        URL.revokeObjectURL(previousUrl);
    }

    const url = URL.createObjectURL(new Blob([png], { type: 'image/png' }));
    thumbnailImageUrls[pageNumber - 1] = url;

    const image = item.querySelector('img');
    image.src = url;
    image.hidden = false;
});

function updateCurrentThumbnail(pageNumber) {
    const item = thumbnailItems[pageNumber - 1] ?? null;
    if (item === currentThumbnailItem) {
        return;
    }

    currentThumbnailItem?.classList.remove('current');
    item?.classList.add('current');
    currentThumbnailItem = item;
}

// ── Contents ─────────────────────────────────────────────────

function buildOutlines(outlines) {
    outlineTree.replaceChildren(createOutlineList(outlines));
}

function createOutlineList(items) {
    const list = document.createElement('ul');
    list.className = 'tree';

    for (const item of items) {
        const listItem = document.createElement('li');
        listItem.className = 'tree-item';
        listItem.classList.toggle('collapsed', !item.isExpanded);

        const row = document.createElement('div');
        row.className = 'tree-row';

        const toggle = createTreeToggle(listItem, item.children.length > 0);

        const label = document.createElement('span');
        label.className = 'tree-label';
        label.textContent = item.title ?? 'Untitled';
        label.title = label.textContent;
        label.style.fontWeight = item.isBold ? 'bold' : '';
        label.style.fontStyle = item.isItalic ? 'italic' : '';
        label.style.color = item.color ?? '';
        label.addEventListener('click', () => pdfPixel.activateOutlineItem(containerId, item.handle));

        row.append(toggle, label);
        listItem.append(row);

        if (item.children.length > 0) {
            listItem.append(createOutlineList(item.children));
        }

        list.append(listItem);
    }

    return list;
}

function createTreeToggle(listItem, hasChildren) {
    const toggle = document.createElement('span');
    toggle.className = 'tree-toggle material-symbols-outlined';
    toggle.textContent = 'expand_more';

    if (!hasChildren) {
        toggle.classList.add('empty');
        return toggle;
    }

    toggle.addEventListener('click', () => listItem.classList.toggle('collapsed'));
    return toggle;
}

// ── Layers ───────────────────────────────────────────────────

function buildLayerConfigurations(configurations) {
    const hasDistinctConfigurations = configurations.length > 1
        || (configurations.length === 1 && configurations[0].name !== null);

    layerConfigurationSelect.hidden = !hasDistinctConfigurations;
    layerConfigurationSelect.replaceChildren(...configurations.map(configuration => {
        const option = document.createElement('option');
        option.value = String(configuration.handle);
        option.textContent = configuration.name ?? 'Unnamed configuration';
        return option;
    }));

    layerTree.replaceChildren();
    layerCheckboxes = new Map();
    renderedLayerConfiguration = null;
}

function updateLayers(state) {
    if (!documentData) {
        return;
    }

    if (state.layerConfiguration !== renderedLayerConfiguration) {
        renderedLayerConfiguration = state.layerConfiguration;
        layerCheckboxes = new Map();

        const configuration = documentData.layerConfigurations.find(item => item.handle === state.layerConfiguration);
        layerTree.replaceChildren(configuration ? createLayerList(configuration.layers) : '');

        if (state.layerConfiguration !== null) {
            layerConfigurationSelect.value = String(state.layerConfiguration);
        }
    }

    const visibleLayers = new Set(state.visibleLayers);
    for (const [handle, checkbox] of layerCheckboxes) {
        checkbox.checked = visibleLayers.has(handle);
    }
}

function createLayerList(layers) {
    const list = document.createElement('ul');
    list.className = 'tree';

    for (const layer of layers) {
        const listItem = document.createElement('li');
        listItem.className = 'tree-item';

        const row = document.createElement('div');
        row.className = 'tree-row';

        if (layer.isLayer) {
            const label = document.createElement('label');
            label.className = 'layer-label';

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.disabled = layer.isLocked;
            checkbox.addEventListener('change', () => pdfPixel.setLayerVisible(containerId, layer.handle, checkbox.checked));
            layerCheckboxes.set(layer.handle, checkbox);

            label.append(checkbox, layer.name ?? 'Unnamed layer');
            row.append(label);
        } else {
            const label = document.createElement('span');
            label.className = 'layer-group';
            label.textContent = layer.name ?? 'Unnamed';
            row.append(label);
        }

        listItem.append(row);

        if (layer.children.length > 0) {
            listItem.append(createLayerList(layer.children));
        }

        list.append(listItem);
    }

    return list;
}

layerConfigurationSelect.addEventListener('change', () =>
    pdfPixel.setLayerConfiguration(containerId, Number(layerConfigurationSelect.value)));

// ── Attachments ──────────────────────────────────────────────

function buildAttachments(attachments) {
    attachmentList.replaceChildren(...attachments.map(attachment => {
        const item = document.createElement('div');
        item.className = 'attachment-item';
        item.title = attachment.file.description ?? '';
        item.addEventListener('dblclick', () => openEmbeddedFile(attachment.file, null));

        const name = document.createElement('div');
        name.textContent = attachment.file.name ?? 'Unnamed file';

        const location = document.createElement('div');
        location.className = 'attachment-location';
        location.textContent = (attachment.pageNumber !== null) ? `Page ${attachment.pageNumber}` : 'Document';

        item.append(name, location);
        return item;
    }));
}

async function saveFile(file) {
    if (file.handle === null) {
        console.log(`${file.name ?? 'The file'} is not embedded in the document.`);
        return;
    }

    const bytes = await pdfPixel.getFileBytes(containerId, file.handle);
    if (!bytes) {
        console.warn(`Embedded file ${file.name} is not read.`);
        return;
    }

    const url = URL.createObjectURL(new Blob([bytes]));
    const link = document.createElement('a');
    link.href = url;
    link.download = file.name ?? 'attachment';
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 0);
}

// ── Panel callbacks ──────────────────────────────────────────

pdfPixel.setOnStateChanged(containerId, (state) => {
    currentState = state;
    const pageCount = documentData ? documentData.pageCount : 0;
    const hasDocument = pageCount > 0;
    pageInput.disabled = !hasDocument;
    if (document.activeElement !== pageInput) {
        pageInput.value = hasDocument ? String(state.currentPage) : '';
        pageInput.max = hasDocument ? String(pageCount) : '';
    }
    pageTotalLabel.textContent = hasDocument ? `/ ${pageCount}` : '/ –';
    prevPageButton.disabled = !hasDocument || state.currentPage <= 1;
    nextPageButton.disabled = !hasDocument || state.currentPage >= pageCount;
    zoomLevelLabel.textContent = `${Math.round(state.scale * 100)}%`;
    autoScaleIcon.textContent = autoScaleIcons[state.autoScaleMode] ?? autoScaleIcons.noAutoScale;

    updatePointerPopup(state);
    updateSearchStatus(state);
    updateCurrentThumbnail(state.currentPage);
    updateLayers(state);
});

pdfPixel.setOnActionRequested(containerId, ({ source, action }) => {
    if (source !== 'linkAnnotationClick' && source !== 'fileAttachmentAnnotationClick' && source !== 'outline') {
        console.log(`${action.type} action from ${source} is not performed.`);
        return;
    }
    switch (action.type) {
        case 'uri':
            openUri(action.uri);
            break;
        case 'launch':
            launch(action);
            break;
        case 'goToRemote':
            goToRemote(action);
            break;
        default:
            console.log(`${action.type === 'unsupported' ? action.actionType : action.type} action is not supported.`);
            break;
    }
});

function openUri(uri) {
    if (!uri) {
        return;
    }

    if (window.confirm(`The document wants to open a link in a new tab:\n\n${uri}`)) {
        window.open(uri, '_blank', 'noopener,noreferrer');
    }
}

function launch(action) {
    if (!action.file) {
        console.warn('Launch action names no file.');
        return;
    }

    if (action.file.handle !== null) {
        openEmbeddedFile(action.file, action.newWindow);
        return;
    }

    const url = resolveFileUrl(action.file);
    if (!url) {
        return;
    }

    if (isPdfFileName(action.file.name)) {
        openDocumentFromUrl(url, null, action.newWindow);
        return;
    }

    if (window.confirm(`The document wants to open a file in a new tab:\n\n${url.href}`)) {
        window.open(url.href, '_blank', 'noopener,noreferrer');
    }
}

async function goToRemote(action) {
    if (action.file && action.file.handle !== null) {
        const bytes = await pdfPixel.getFileBytes(containerId, action.file.handle);
        if (bytes) {
            openDocument({ bytes, fileName: action.file.name, baseUrl: null }, action.destination, action.newWindow);
        }
        return;
    }

    const url = resolveFileUrl(action.file);
    if (url) {
        openDocumentFromUrl(url, action.destination, action.newWindow);
    }
}

pdfPixel.setOnFramePresented(containerId, ({ canvas, frame }) => {
    const context = canvas.getContext('2d');
    const { width, height } = frame.panelSize;

    context.clearRect(0, 0, width, height);
    drawLogo(context, width, height);
});

function drawLogo(context, panelWidth, panelHeight) {
    const cellSize = 18;
    const padding = 4;

    context.save();
    context.translate(panelWidth - 58, panelHeight - 48);
    context.scale(0.5, 0.5);

    for (let column = 0; column < 3; column++) {
        for (let row = 0; row < 3; row++) {
            const isCenter = column === 1 && row === 1;
            context.fillStyle = isCenter ? 'rgba(70, 149, 235, 0.5)' : 'rgba(33, 35, 43, 0.5)';
            context.fillRect(column * (cellSize + padding), row * (cellSize + padding), cellSize, cellSize);
        }
    }

    context.restore();
}

// ── Header ───────────────────────────────────────────────────

prevPageButton.addEventListener('click', () => {
    if (currentState.currentPage > 1) {
        pdfPixel.setPage(containerId, currentState.currentPage - 1);
    }
});

nextPageButton.addEventListener('click', () => {
    if (documentData && currentState.currentPage < documentData.pageCount) {
        pdfPixel.setPage(containerId, currentState.currentPage + 1);
    }
});

pageInput.addEventListener('change', () => {
    const requestedPage = parseInt(pageInput.value, 10);
    if (documentData && !isNaN(requestedPage) && requestedPage >= 1 && requestedPage <= documentData.pageCount) {
        pdfPixel.setPage(containerId, requestedPage);
    } else {
        pageInput.value = String(currentState.currentPage);
    }
});

pageInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        pageInput.blur();
    }
});

zoomOutButton.addEventListener('click', () => {
    pdfPixel.setScale(containerId, currentState.scale * 0.9);
});

zoomInButton.addEventListener('click', () => {
    pdfPixel.setScale(containerId, currentState.scale * 1.1);
});

resetZoomButton.addEventListener('click', () => {
    pdfPixel.setScale(containerId, 1.0);
});

autoScaleButton.addEventListener('click', () => {
    const mode = (currentState.autoScaleMode === 'scaleToWidth') ? 'scaleToHeight' : 'scaleToWidth';
    pdfPixel.setAutoScaleMode(containerId, mode);
});

function applyPageRotation(pageNumber) {
    const degrees = (allPagesRotation + (pageRotations.get(pageNumber) ?? 0)) % 360;
    pdfPixel.setPageRotation(containerId, pageNumber, degrees);
}

rotateAllPagesButton.addEventListener('click', () => {
    allPagesRotation = (allPagesRotation + 90) % 360;
    pdfPixel.setRotation(containerId, allPagesRotation);

    for (const pageNumber of pageRotations.keys()) {
        applyPageRotation(pageNumber);
    }
});

rotatePageButton.addEventListener('click', () => {
    const pageNumber = currentState.currentPage;
    if (!documentData || pageNumber < 1) {
        return;
    }

    pageRotations.set(pageNumber, ((pageRotations.get(pageNumber) ?? 0) + 90) % 360);
    applyPageRotation(pageNumber);
});

loadPdfBtn.addEventListener('click', () => {
    pdfFileInput.value = '';
    pdfFileInput.click();
});

pdfFileInput.addEventListener('change', async (event) => {
    const file = event.target.files && event.target.files[0];
    if (!file) {
        return;
    }
    const arrayBuffer = await file.arrayBuffer();
    loadDocument({ bytes: new Uint8Array(arrayBuffer), fileName: file.name, baseUrl: null }, null);
});

// ── Startup ──────────────────────────────────────────────────

const startupFile = new URLSearchParams(location.search).get('file');

if (location.hash === '#child' && window.opener) {
    window.opener.postMessage({ type: 'pdf-pixel-ready' }, location.origin);
} else if (startupFile) {
    openDocumentFromUrl(new URL(startupFile, location.href), null, false);
}
