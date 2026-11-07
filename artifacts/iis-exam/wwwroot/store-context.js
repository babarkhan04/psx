const key = "SmartPOS.SelectedStoreId";
const cookieName = "SmartPOS.SelectedStoreId";

export function getSelectedStoreId() {
    try {
        const fromLocal = window.localStorage.getItem(key);
        if (fromLocal) {
            return fromLocal;
        }

        const cookie = document.cookie
            .split(";")
            .map(x => x.trim())
            .find(x => x.startsWith(cookieName + "="));

        return cookie ? decodeURIComponent(cookie.substring(cookieName.length + 1)) : null;
    } catch {
        return null;
    }
}

export function setSelectedStoreId(storeId) {
    if (!storeId) {
        clearSelectedStoreId();
        return;
    }

    try {
        window.localStorage.setItem(key, storeId);
    } catch { }

    const maxAge = 60 * 60 * 24 * 30;
    document.cookie = `${cookieName}=${encodeURIComponent(storeId)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

export function clearSelectedStoreId() {
    try {
        window.localStorage.removeItem(key);
    } catch { }

    document.cookie = `${cookieName}=; path=/; max-age=0; SameSite=Lax`;
}
