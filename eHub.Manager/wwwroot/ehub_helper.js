async function setToClipboard(text) {
    await navigator.clipboard.writeText(text);
}
