(function () {
    window.smartPosLabels = {
        renderAll: function () {
            document.querySelectorAll('svg.sp-label-barcode[data-barcode]').forEach(svg => {
                renderCode128(svg, svg.getAttribute('data-barcode') || '');
            });
        },
        print: function (elementId, title) {
            this.renderAll();
            const source = document.getElementById(elementId);
            if (!source) { window.print(); return; }
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
            doc.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title || 'Labels')}</title>${css}</head><body>${source.outerHTML}</body></html>`);
            doc.close();
            setTimeout(() => {
                iframe.contentWindow.smartPosLabels = window.smartPosLabels;
                iframe.contentWindow.document.querySelectorAll('svg.sp-label-barcode[data-barcode]').forEach(svg => renderCode128(svg, svg.getAttribute('data-barcode') || ''));
                try { iframe.contentWindow.focus(); iframe.contentWindow.print(); }
                finally { setTimeout(() => iframe.remove(), 1000); }
            }, 250);
        }
    };

    const patterns = [
        '11011001100','11001101100','11001100110','10010011000','10010001100','10001001100','10011001000','10011000100','10001100100','11001001000',
        '11001000100','11000100100','10110011100','10011011100','10011001110','10111001100','10011101100','10011100110','11001110010','11001011100',
        '11001001110','11011100100','11001110100','11101101110','11101001100','11100101100','11100100110','11101100100','11100110100','11100110010',
        '11011011000','11011000110','11000110110','10100011000','10001011000','10001000110','10110001000','10001101000','10001100010','11010001000',
        '11000101000','11000100010','10110111000','10110001110','10001101110','10111011000','10111000110','10001110110','11101110110','11010001110',
        '11000101110','11011101000','11011100010','11011101110','11101011000','11101000110','11100010110','11101101000','11101100010','11100011010',
        '11101111010','11001000010','11110001010','10100110000','10100001100','10010110000','10010000110','10000101100','10000100110','10110010000',
        '10110000100','10011010000','10011000010','10000110100','10000110010','11000010010','11001010000','11110111010','11000010100','10001111010',
        '10100111100','10010111100','10010011110','10111100100','10011110100','10011110010','11110100100','11110010100','11110010010','11011011110',
        '11011110110','11110110110','10101111000','10100011110','10001011110','10111101000','10111100010','11110101000','11110100010','10111011110',
        '10111101110','11101011110','11110101110','11010000100','11010010000','11010011100','11000111010'
    ];

    function renderCode128(svg, value) {
        value = String(value || '').trim();
        if (!value) { svg.innerHTML = ''; return; }
        // Code 128-B: start 104, printable ASCII value = charCode - 32, stop 106.
        const codes = [104];
        for (const ch of value) {
            const code = ch.charCodeAt(0);
            codes.push(code >= 32 && code <= 126 ? code - 32 : 0);
        }
        let checksum = 104;
        for (let i = 1; i < codes.length; i++) checksum += codes[i] * i;
        codes.push(checksum % 103, 106);
        const bits = codes.map(c => patterns[c]).join('') + '11';
        const width = bits.length;
        const height = 36;
        let x = 0;
        let rects = '';
        for (let i = 0; i < bits.length; i++) {
            if (bits[i] === '1') rects += `<rect x="${x}" y="0" width="1" height="${height}"/>`;
            x++;
        }
        svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
        svg.setAttribute('preserveAspectRatio', 'none');
        svg.innerHTML = `<g fill="#111827">${rects}</g>`;
    }

    function escapeHtml(value) {
        return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    }
})();
