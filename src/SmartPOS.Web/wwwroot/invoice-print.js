(function () {
    window.smartPosInvoicePrint = {
        printElement: function (elementId, title) {
            const source = document.getElementById(elementId);
            if (!source) {
                window.print();
                return;
            }
            const iframe = document.createElement('iframe');
            iframe.style.position = 'fixed';
            iframe.style.right = '0';
            iframe.style.bottom = '0';
            iframe.style.width = '0';
            iframe.style.height = '0';
            iframe.style.border = '0';
            iframe.style.opacity = '0';
            document.body.appendChild(iframe);
            const css = Array.from(document.querySelectorAll('link[rel="stylesheet"], style')).map(x => x.outerHTML).join('\n');
            const doc = iframe.contentWindow.document;
            doc.open();
            doc.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title || 'Invoice')}</title>${css}</head><body>${source.outerHTML}</body></html>`);
            doc.close();
            setTimeout(() => {
                try {
                    iframe.contentWindow.focus();
                    iframe.contentWindow.print();
                } finally {
                    setTimeout(() => iframe.remove(), 1000);
                }
            }, 250);
        }
    };

    function escapeHtml(value) {
        return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    }
})();
