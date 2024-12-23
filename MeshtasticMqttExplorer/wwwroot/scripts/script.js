window.copyTextToClipboard = (text) => {
    if (navigator?.clipboard?.writeText) {
        return navigator.clipboard.writeText(text);
    }
    return Promise.reject('The Clipboard API is not available.');
};

window.downloadFile = (type, filename, content) => {
    const element = document.createElement('a');
    element.setAttribute('href', type + ',' + encodeURIComponent(content));
    element.setAttribute('download', filename);

    element.style.display = 'none';
    document.body.appendChild(element);

    element.click();

    document.body.removeChild(element);
};