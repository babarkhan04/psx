let handler;

export function register(dotNetRef) {
    handler = (event) => {
        const isK = event.key === 'k' || event.key === 'K';
        if ((event.ctrlKey || event.metaKey) && isK) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync('OpenSearch');
        }
    };

    document.addEventListener('keydown', handler);
}

export function dispose() {
    if (handler) {
        document.removeEventListener('keydown', handler);
        handler = undefined;
    }
}
