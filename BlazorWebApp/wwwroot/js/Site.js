window.clipboardCopy = {
  copyText: function (text) {
    navigator.clipboard.writeText(text).catch(function (error) {
      alert(error);
    });
  },
};

window.workshopChat = {
  scrollToBottom: function (element, smooth) {
    if (!element) return;
    try {
      element.scrollTo({
        top: element.scrollHeight,
        behavior: smooth ? "smooth" : "auto",
      });
    } catch (e) {
      element.scrollTop = element.scrollHeight;
    }
  },
};

window.downloadFile = function (filename, content, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

window.assetViewerCompare = {
  getRect: function (el) {
    if (!el || !el.getBoundingClientRect) return { left: 0, width: 1 };
    const r = el.getBoundingClientRect();
    return { left: r.left, width: r.width };
  },
};
