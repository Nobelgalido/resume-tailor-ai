// Triggers a browser file save from base64-encoded bytes handed over via JS interop.
// There is no built-in browser API for "save these bytes as a file", so this uses the
// standard Blob + anchor[download] pattern.
window.downloadFileFromBytes = (fileName, contentType, base64Data) => {
    const bytes = atob(base64Data);
    const buffer = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) {
        buffer[i] = bytes.charCodeAt(i);
    }

    const blob = new Blob([buffer], { type: contentType });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    URL.revokeObjectURL(url);
};
