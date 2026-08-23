(function () {
    const printStyles = `
        * { box-sizing: border-box; }
        html, body { margin: 0; padding: 0; background: #fff; color: #111827; font-family: Arial, sans-serif; }
        body.thermal { width: 80mm; }
        body.a4 { width: 210mm; padding: 12mm; }
        .sp-receipt-preview-toolbar, .sp-receipt-print-actions, .rz-button { display: none !important; }
        .sp-print-receipt { padding: 0; font-size: 10.5px; line-height: 1.3; color: #111827; background: #fff; }
        body.a4 .sp-print-receipt { max-width: 148mm; margin: 0 auto; padding: 10mm; border: 1px solid #d1d5db; font-size: 12px; }
        .sp-print-receipt-preview { border: 0 !important; box-shadow: none !important; max-height: none !important; overflow: visible !important; border-radius: 0 !important; }
        .sp-print-receipt-header { text-align: center; }
        .sp-print-receipt-brand { font-size: 16px; line-height: 1.15; font-weight: 800; letter-spacing: -0.02em; }
        body.a4 .sp-print-receipt-brand { font-size: 22px; }
        .sp-print-receipt-subtitle, .sp-print-receipt-number, .sp-print-receipt-footer small, .sp-print-receipt-item small { color: #374151; }
        .sp-print-receipt-divider { border-top: 1px dashed #333; margin: 8px 0; }
        .sp-print-receipt-number { font-weight: 700; font-size: 11px; }
        .sp-print-receipt-duplicate { display: inline-block; margin-top: 5px; padding: 2px 6px; border: 1px solid #111; border-radius: 999px; font-size: 9px; font-weight: 800; letter-spacing: 0.04em; }
        .sp-print-receipt-meta-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4px 8px; margin: 10px 0; padding: 0; }
        .sp-print-receipt-meta-grid span { display: block; color: #4b5563; font-size: 8.5px; text-transform: uppercase; letter-spacing: 0.03em; }
        .sp-print-receipt-meta-grid strong { display: block; color: #111827; font-size: 9.5px; word-break: break-word; }
        body.a4 .sp-print-receipt-meta-grid span { font-size: 10px; }
        body.a4 .sp-print-receipt-meta-grid strong { font-size: 12px; }
        .sp-print-receipt-meta-wide { grid-column: 1 / -1; }
        .sp-print-receipt-items { border-top: 1px dashed #333; border-bottom: 1px dashed #333; padding: 6px 0; }
        .sp-print-receipt-items-head, .sp-print-receipt-item { display: grid; grid-template-columns: minmax(0, 1fr) 28px 52px; gap: 5px; align-items: start; }
        body.a4 .sp-print-receipt-items-head, body.a4 .sp-print-receipt-item { grid-template-columns: minmax(0, 1fr) 45px 85px; gap: 8px; }
        .sp-print-receipt-items-head { color: #374151; font-size: 8.5px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.04em; padding-bottom: 5px; }
        .sp-print-receipt-items-head span:nth-child(2), .sp-print-receipt-items-head span:nth-child(3), .sp-print-receipt-item-qty, .sp-print-receipt-item-total { text-align: right; }
        .sp-print-receipt-item { padding: 5px 0; border-top: 1px solid #e5e7eb; break-inside: avoid; }
        .sp-print-receipt-item:first-of-type { border-top: 0; }
        .sp-print-receipt-item-name strong { display: block; font-size: 10px; font-weight: 800; }
        .sp-print-receipt-item-name small { display: block; font-size: 8.5px; margin-top: 1px; }
        body.a4 .sp-print-receipt-item-name strong { font-size: 12px; }
        body.a4 .sp-print-receipt-item-name small { font-size: 10px; }
        .sp-print-receipt-totals { margin-top: 8px; }
        .sp-print-receipt-totals div { display: flex; justify-content: space-between; gap: 8px; padding: 2px 0; }
        .sp-print-receipt-totals span { color: #374151; }
        .sp-print-receipt-grand { margin-top: 5px; padding-top: 6px !important; border-top: 2px solid #111; font-size: 13px; font-weight: 900; }
        body.a4 .sp-print-receipt-grand { font-size: 16px; }
        .sp-print-receipt-grand span, .sp-print-receipt-grand strong { color: #111827; }
        .sp-print-receipt-footer { text-align: center; margin-top: 10px; padding-top: 8px; border-top: 1px dashed #333; }
        .sp-print-receipt-code { display: inline-flex; flex-direction: column; gap: 1px; min-width: 42mm; padding: 5px 8px; border: 1px dashed #333; border-radius: 6px; margin-bottom: 6px; }
        .sp-print-receipt-code span { color: #374151; font-size: 8px; text-transform: uppercase; letter-spacing: 0.06em; }
        .sp-print-receipt-code strong { font-size: 10px; }
        .sp-print-receipt-footer p { margin: 3px 0; font-weight: 800; }
        .sp-print-receipt-footer small { display: block; font-size: 8.5px; margin-top: 3px; }
        body.a4 .sp-print-receipt-footer small { font-size: 10px; }
        .sp-print-receipt-system { margin-top: 6px !important; }
        @page { margin: 3mm; }
        body.thermal { print-color-adjust: exact; -webkit-print-color-adjust: exact; }
        body.thermal @page { size: 80mm auto; margin: 3mm; }
    `;

    window.smartPosReceiptPrinter = {
        printElement: function (elementId, mode, title) {
            const source = document.getElementById(elementId);
            if (!source) {
                window.print();
                return;
            }

            const printMode = mode === 'a4' ? 'a4' : 'thermal';
            const iframe = document.createElement('iframe');
            iframe.setAttribute('title', 'SmartPOS Receipt Print');
            iframe.style.position = 'fixed';
            iframe.style.right = '0';
            iframe.style.bottom = '0';
            iframe.style.width = '0';
            iframe.style.height = '0';
            iframe.style.border = '0';
            iframe.style.opacity = '0';
            document.body.appendChild(iframe);

            const doc = iframe.contentWindow.document;
            doc.open();
            doc.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title || 'Receipt')}</title><style>${printStyles}</style></head><body class="${printMode}">${source.outerHTML}</body></html>`);
            doc.close();

            window.setTimeout(() => {
                try {
                    iframe.contentWindow.focus();
                    iframe.contentWindow.print();
                } finally {
                    window.setTimeout(() => iframe.remove(), 1200);
                }
            }, 250);
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
