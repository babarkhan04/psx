(function () {
    function normalizeMode(mode) {
        const value = String(mode || 'thermal').toLowerCase();
        if (value === 'a4') return 'a4 smartpos-print-body';
        if (value === '58mm') return 'thermal compact58 smartpos-print-body';
        return 'thermal smartpos-print-body';
    }

    function pageCss(mode) {
        const value = String(mode || 'thermal').toLowerCase();
        if (value === 'a4') {
            return '@page { size: A4 portrait; margin: 12mm; }';
        }
        if (value === '58mm') {
            return '@page { size: 58mm auto; margin: 2mm; }';
        }
        return '@page { size: 80mm auto; margin: 3mm; }';
    }

    function printCss(mode) {
        return `
${pageCss(mode)}
* { box-sizing: border-box; }
html, body { background: #fff !important; color: #000 !important; margin: 0 !important; padding: 0 !important; font-family: Arial, Helvetica, sans-serif !important; }
body.smartpos-print-body { display: block !important; }
body.thermal .sp-print-receipt, body.thermal .sp-print-receipt-preview, body.thermal #receiptDesignerPreview {
    width: 72mm !important; max-width: 72mm !important; margin: 0 auto !important; padding: 3mm !important;
    border: 0 !important; box-shadow: none !important; background: #fff !important; color: #000 !important;
    font-size: 11px !important; line-height: 1.25 !important;
}
body.compact58 .sp-print-receipt, body.compact58 .sp-print-receipt-preview, body.compact58 #receiptDesignerPreview {
    width: 52mm !important; max-width: 52mm !important; padding: 2.5mm !important; font-size: 9px !important; line-height: 1.2 !important;
}
body.a4 .sp-print-receipt, body.a4 .sp-print-receipt-preview, body.a4 #receiptDesignerPreview {
    width: 180mm !important; max-width: 180mm !important; margin: 0 auto !important; padding: 8mm !important;
    border: 1px solid #e5e7eb !important; font-size: 12px !important; line-height: 1.35 !important;
}
.sp-print-receipt-header { text-align: center !important; margin-bottom: 6px !important; }
.sp-print-receipt-brand { font-size: 15px !important; font-weight: 800 !important; text-transform: uppercase !important; }
.sp-print-receipt-subtitle { font-size: 10px !important; color: #111 !important; margin-top: 2px !important; }
.sp-print-receipt-divider { border-top: 1px dashed #111 !important; margin: 6px 0 !important; height: 0 !important; }
.sp-print-receipt-number { font-weight: 800 !important; font-size: 11px !important; margin-top: 4px !important; }
.sp-print-receipt-meta-grid { display: grid !important; grid-template-columns: 1fr 1fr !important; gap: 3px 8px !important; margin: 6px 0 !important; }
.sp-print-receipt-meta-grid > div { display: flex !important; justify-content: space-between !important; gap: 6px !important; min-width: 0 !important; }
.sp-print-receipt-meta-grid span { color: #333 !important; }
.sp-print-receipt-meta-grid strong { font-weight: 700 !important; text-align: right !important; }
.sp-print-receipt-meta-wide { grid-column: 1 / -1 !important; }
.sp-print-receipt-items { border-top: 1px dashed #111 !important; border-bottom: 1px dashed #111 !important; margin: 7px 0 !important; padding: 5px 0 !important; }
.sp-print-receipt-items-head, .sp-print-receipt-item { display: grid !important; grid-template-columns: 1fr 28px 54px !important; gap: 6px !important; align-items: start !important; }
.sp-print-receipt-items-head { font-weight: 800 !important; margin-bottom: 4px !important; }
.sp-print-receipt-item { margin: 4px 0 !important; page-break-inside: avoid !important; }
.sp-print-receipt-item-name strong { display: block !important; font-weight: 700 !important; }
.sp-print-receipt-item-name small { display: block !important; color: #333 !important; font-size: 9px !important; margin-top: 1px !important; }
.sp-print-receipt-item-qty, .sp-print-receipt-item-total { text-align: right !important; white-space: nowrap !important; }
.sp-print-receipt-totals { margin-top: 6px !important; }
.sp-print-receipt-totals > div { display: flex !important; justify-content: space-between !important; gap: 8px !important; margin: 3px 0 !important; }
.sp-print-receipt-grand { border-top: 1px solid #111 !important; padding-top: 5px !important; margin-top: 5px !important; font-size: 13px !important; font-weight: 800 !important; }
.sp-print-receipt-footer { text-align: center !important; margin-top: 8px !important; border-top: 1px dashed #111 !important; padding-top: 6px !important; }
.sp-print-receipt-footer p { margin: 3px 0 !important; font-weight: 700 !important; }
.sp-print-receipt-footer small { display: block !important; color: #333 !important; font-size: 9px !important; }
.sp-print-receipt-code { border: 1px solid #111 !important; padding: 4px !important; margin: 4px 0 6px !important; display: flex !important; justify-content: space-between !important; }
@media print { body { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; } }
`;
    }

    function printViaBrowser(elementId, mode, title) {
        const source = document.getElementById(elementId);
        if (!source) {
            window.print();
            return;
        }

        const iframe = document.createElement('iframe');
        iframe.setAttribute('title', 'SmartPOS Print Station');
        iframe.style.position = 'fixed';
        iframe.style.left = '-10000px';
        iframe.style.top = '0';
        iframe.style.width = '900px';
        iframe.style.height = '1200px';
        iframe.style.border = '0';
        document.body.appendChild(iframe);

        const printMode = String(mode || 'thermal').toLowerCase();
        const doc = iframe.contentWindow.document;
        doc.open();
        doc.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title || 'SmartPOS Print')}</title><style>${printCss(printMode)}</style></head><body class="${normalizeMode(printMode)}">${source.outerHTML}</body></html>`);
        doc.close();

        window.setTimeout(() => {
            try {
                void iframe.contentWindow.document.body.offsetHeight;
                iframe.contentWindow.focus();
                iframe.contentWindow.print();
            } finally {
                window.setTimeout(() => iframe.remove(), 1500);
            }
        }, 350);
    }

    function shiftReportCss() {
        return `
@page { size: A4 portrait; margin: 12mm; }
* { box-sizing: border-box; }
html, body {
    background: #fff !important;
    color: #111827 !important;
    margin: 0 !important;
    padding: 0 !important;
    font-family: Arial, Helvetica, sans-serif !important;
    font-size: 12px !important;
    line-height: 1.35 !important;
}
body { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
.printable-shift-report {
    width: 100% !important;
    max-width: 190mm !important;
    margin: 0 auto !important;
    padding: 0 !important;
    background: #fff !important;
    color: #111827 !important;
}
.shift-report-title {
    display: flex !important;
    align-items: flex-start !important;
    justify-content: space-between !important;
    gap: 16px !important;
    padding-bottom: 12px !important;
    margin-bottom: 12px !important;
    border-bottom: 2px solid #111827 !important;
}
.shift-report-title h2 {
    margin: 0 !important;
    font-size: 22px !important;
    line-height: 1.15 !important;
    color: #111827 !important;
}
.shift-report-title p {
    margin: 4px 0 0 !important;
    color: #475569 !important;
    font-size: 11px !important;
}
.status-pill {
    display: inline-block !important;
    padding: 4px 9px !important;
    border: 1px solid #94a3b8 !important;
    border-radius: 999px !important;
    font-size: 10px !important;
    font-weight: 700 !important;
    text-transform: uppercase !important;
    background: #fff !important;
    color: #111827 !important;
}
.receive-payment-summary {
    display: grid !important;
    grid-template-columns: repeat(4, minmax(0, 1fr)) !important;
    gap: 8px !important;
    margin-bottom: 12px !important;
}
.receive-payment-summary > div {
    border: 1px solid #d1d5db !important;
    border-radius: 7px !important;
    padding: 8px !important;
    min-width: 0 !important;
    background: #fff !important;
}
.receive-payment-summary span,
.kpi-label {
    display: block !important;
    color: #64748b !important;
    font-size: 9px !important;
    text-transform: uppercase !important;
    letter-spacing: .04em !important;
}
.receive-payment-summary strong {
    display: block !important;
    margin-top: 3px !important;
    color: #111827 !important;
    font-size: 11px !important;
    overflow-wrap: anywhere !important;
}
.kpi-grid {
    display: grid !important;
    grid-template-columns: repeat(4, minmax(0, 1fr)) !important;
    gap: 8px !important;
    margin: 0 0 14px !important;
}
.kpi-card {
    border: 1px solid #d1d5db !important;
    border-radius: 7px !important;
    padding: 9px !important;
    background: #fff !important;
    box-shadow: none !important;
    min-width: 0 !important;
}
.kpi-value {
    margin-top: 4px !important;
    font-size: 16px !important;
    font-weight: 800 !important;
    color: #111827 !important;
}
.text-warn { color: #991b1b !important; }
.text-ok { color: #166534 !important; }
.shift-report-section-title {
    margin: 14px 0 6px !important;
    padding-bottom: 4px !important;
    border-bottom: 1px solid #cbd5e1 !important;
    font-size: 12px !important;
    font-weight: 800 !important;
    color: #111827 !important;
}
.shift-report-table {
    width: 100% !important;
    border-collapse: collapse !important;
    table-layout: fixed !important;
    font-size: 11px !important;
    page-break-inside: avoid !important;
}
.shift-report-table th,
.shift-report-table td {
    padding: 6px 7px !important;
    border: 1px solid #d1d5db !important;
    background: #fff !important;
    color: #111827 !important;
}
.shift-report-table th {
    background: #f3f4f6 !important;
    font-size: 10px !important;
    font-weight: 800 !important;
    text-align: left !important;
}
.shift-report-table td:last-child,
.shift-report-table th:last-child {
    text-align: right !important;
}
.denomination-report-table th,
.denomination-report-table td { text-align: right !important; }
.denomination-report-table th:first-child,
.denomination-report-table td:first-child { text-align: left !important; }
.no-print, button, .rz-button { display: none !important; }
@media print {
    html, body { width: 100% !important; }
    .printable-shift-report { page-break-inside: auto !important; }
}
`;
    }

    function printShiftReport(elementId, title) {
        const source = document.getElementById(elementId);
        if (!source) {
            console.warn('SmartPOS shift report print area was not found:', elementId);
            return false;
        }

        const iframe = document.createElement('iframe');
        iframe.setAttribute('title', 'SmartPOS Shift Report Print');
        iframe.style.position = 'fixed';
        iframe.style.left = '-10000px';
        iframe.style.top = '0';
        iframe.style.width = '900px';
        iframe.style.height = '1200px';
        iframe.style.border = '0';
        iframe.style.opacity = '0';
        iframe.style.pointerEvents = 'none';
        document.body.appendChild(iframe);

        const doc = iframe.contentWindow.document;
        doc.open();
        doc.write(`<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${escapeHtml(title || 'Shift Report')}</title><style>${shiftReportCss()}</style></head><body>${source.outerHTML}</body></html>`);
        doc.close();

        window.setTimeout(() => {
            try {
                iframe.contentWindow.focus();
                iframe.contentWindow.print();
            } finally {
                window.setTimeout(() => iframe.remove(), 1500);
            }
        }, 300);

        return true;
    }

    async function postToAgent(path, payload, settings) {
        const base = (settings && settings.localPrintAgentUrl) || (settings && settings.LocalPrintAgentUrl) || 'http://localhost:3859';
        const url = String(base).replace(/\/$/, '') + path;
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        return response.ok;
    }

    window.smartPosPrintStation = {
        printElement: async function (elementId, mode, title, settings) {
            settings = settings || {};
            const useAgent = settings.useLocalPrintAgent === true || settings.UseLocalPrintAgent === true;
            const useBrowser = settings.useBrowserPrint !== false && settings.UseBrowserPrint !== false;
            const source = document.getElementById(elementId);
            const html = source ? source.outerHTML : '';

            if (useAgent) {
                try {
                    const ok = await postToAgent('/print', {
                        title: title || 'SmartPOS Print',
                        mode: mode || settings.defaultReceiptMode || settings.DefaultReceiptMode || 'thermal',
                        printerName: settings.receiptPrinterName || settings.ReceiptPrinterName || null,
                        html: html,
                        cashDrawerCommandHex: settings.cashDrawerCommandHex || settings.CashDrawerCommandHex || null
                    }, settings);
                    if (ok) return true;
                } catch (err) {
                    console.warn('SmartPOS local print agent unavailable. Falling back to browser print.', err);
                }
            }

            if (useBrowser) {
                printViaBrowser(elementId, mode || settings.defaultReceiptMode || settings.DefaultReceiptMode || 'thermal', title);
                return true;
            }

            return false;
        },

        printShiftReport: function (elementId, title) {
            return printShiftReport(elementId, title);
        },

        kickDrawer: async function (settings) {
            settings = settings || {};
            const enabled = settings.enableCashDrawerKick === true || settings.EnableCashDrawerKick === true;
            const useAgent = settings.useLocalPrintAgent === true || settings.UseLocalPrintAgent === true;
            if (!enabled || !useAgent) return false;

            try {
                return await postToAgent('/drawer/kick', {
                    printerName: settings.receiptPrinterName || settings.ReceiptPrinterName || null,
                    commandHex: settings.cashDrawerCommandHex || settings.CashDrawerCommandHex || '1B700019FA'
                }, settings);
            } catch (err) {
                console.warn('SmartPOS cash drawer kick failed.', err);
                return false;
            }
        }
    };

    window.smartPosReceiptPrinter = window.smartPosReceiptPrinter || {
        printElement: function (elementId, mode, title) {
            return window.smartPosPrintStation.printElement(elementId, mode, title, { useBrowserPrint: true });
        }
    };

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }
})();
