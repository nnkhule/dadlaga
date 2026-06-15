window.scrollElementToBottom = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};