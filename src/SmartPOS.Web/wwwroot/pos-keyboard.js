let handler;
let shortcutMap = [];

function isTypingTarget(target) {
    return target?.matches?.('input, textarea, select, [contenteditable="true"], .rz-textbox, .rz-inputtext, .rz-dropdown, .rz-numeric input');
}

function normalizeKey(key) {
    if (!key) return '';
    if (key.length === 1) return key.toUpperCase();
    return key;
}

function isFunctionKey(key) {
    return /^F\d{1,2}$/i.test(key || '');
}

function sameShortcut(event, shortcut) {
    const key = normalizeKey(event.key || '');
    const configuredKey = normalizeKey(shortcut.key || '');

    // Keyboard layouts can report plus as '=' when Shift is not considered.
    const keyMatches = key === configuredKey ||
        (configuredKey === '+' && (key === '+' || key === '='));

    const shiftMatches = configuredKey === '+' && !shortcut.shift
        ? true
        : !!event.shiftKey === !!shortcut.shift;

    return keyMatches &&
        !!event.ctrlKey === !!shortcut.ctrl &&
        !!event.altKey === !!shortcut.alt &&
        shiftMatches;
}

function normalizeShortcut(event) {
    const typing = isTypingTarget(event.target);

    for (const shortcut of shortcutMap) {
        if (!shortcut?.isEnabled || !shortcut?.methodName) continue;
        if (!sameShortcut(event, shortcut)) continue;

        if (typing && !shortcut.allowWhenTyping && !isFunctionKey(shortcut.key) && !shortcut.alt) {
            return null;
        }

        return shortcut.methodName;
    }

    return null;
}

export function register(dotNetRef, shortcuts) {
    dispose();

    shortcutMap = Array.isArray(shortcuts) ? shortcuts : [];

    // Safe fallback if the settings service has not loaded yet.
    if (shortcutMap.length === 0) {
        shortcutMap = [
            { methodName: 'ShowShortcutHelp', key: 'F1', allowWhenTyping: true, isEnabled: true },
            { methodName: 'FocusSearch', key: 'F2', allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerCustomerSelect', key: 'F4', allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerPaymentMenu', key: 'F8', allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerReprintLastReceipt', key: 'F9', allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerCashPay', key: 'F10', allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerHoldSale', key: 'H', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerRetrieveHeldSale', key: 'G', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerInvoiceDiscount', key: 'D', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerQuickQuantity', key: 'Q', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerFullScreenLock', key: 'L', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerClearCart', key: 'C', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerCustomerDisplay', key: 'J', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerShiftMenu', key: 'S', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerXReport', key: 'X', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerZReport', key: 'Z', alt: true, allowWhenTyping: true, isEnabled: true },
            { methodName: 'TriggerRemoveSelectedLine', key: 'Delete', isEnabled: true },
            { methodName: 'TriggerIncreaseSelectedLine', key: '+', isEnabled: true },
            { methodName: 'TriggerDecreaseSelectedLine', key: '-', isEnabled: true }
        ];
    }

    handler = (event) => {
        const dialogs = document.querySelectorAll('.rz-dialog');
        if (dialogs.length) {
            if (normalizeShortcut(event) === 'TriggerCustomerSelect') {
                const activeDialog = dialogs[dialogs.length - 1];
                const search = activeDialog.querySelector('[data-pos-customer-search]');
                const action = activeDialog.querySelector('[data-pos-customer-action]');
                if (search || (action && !action.disabled)) {
                    event.preventDefault();
                    event.stopPropagation();
                    if (search) { search.focus(); search.select(); }
                    else action.click();
                }
            }
            return;
        }
        const method = normalizeShortcut(event);
        if (!method) return;

        event.preventDefault();
        event.stopPropagation();

        dotNetRef.invokeMethodAsync(method).catch(() => {
            // Blazor circuit may already be disposed during navigation/reload.
        });
    };

    document.addEventListener('keydown', handler, true);
}

export function clickFirst(selector) {
    const el = document.querySelector(selector);
    if (!el) return;
    el.focus?.();
    el.click?.();
}

export function openWindow(url) {
    window.open(url, '_blank', 'noopener,noreferrer');
}

export function dispose() {
    if (handler) {
        document.removeEventListener('keydown', handler, true);
        handler = undefined;
    }
    shortcutMap = [];
}


export async function setPosLockMode(enabled) {
    const body = document.body;
    const root = document.documentElement;

    body.classList.toggle('sp-pos-lock-mode', !!enabled);

    if (enabled) {
        // Use the whole document as the fullscreen element, not the POS panel.
        // Radzen dialogs are rendered under <body>; when <html> is fullscreen,
        // dialogs remain inside the fullscreen tree and can appear above POS.
        if (!document.fullscreenElement && root.requestFullscreen) {
            try {
                await root.requestFullscreen();
                body.classList.add('sp-native-fullscreen');
            } catch {
                // Browser may block fullscreen if not triggered directly by a user gesture.
                // CSS lock mode remains active as fallback.
                body.classList.remove('sp-native-fullscreen');
            }
        }
        return;
    }

    body.classList.remove('sp-native-fullscreen');

    if (document.fullscreenElement && document.exitFullscreen) {
        try {
            await document.exitFullscreen();
        } catch {
            // Ignore browser fullscreen exit failures. CSS lock mode is already off.
        }
    }
}

export function isFullscreen() {
    return document.body.classList.contains('sp-pos-lock-mode');
}
