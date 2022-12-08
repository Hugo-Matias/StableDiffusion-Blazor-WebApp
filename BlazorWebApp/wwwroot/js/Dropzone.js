export function init(dropzoneElement, inputFile) {
  function onDragHover(e) {
    e.preventDefault();
    dropzoneElement.classList.add("hover");
  }

  function onDragLeave(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");
  }

  function onDrop(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");

    inputFile.files = e.dataTransfer.files;
    const event = new Event("change", { bubbles: true });
    inputFile.dispatchEvent(event);
  }

  function onDropWindow(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");
  }

  function onPaste(e) {
    inputFile.files = e.clipboardData.files;
    const event = new Event("change", { bubbles: true });
    inputFile.dispatchEvent(event);
  }

  window.addEventListener("dragenter", onDragHover);
  window.addEventListener("dragover", onDragHover);
  window.addEventListener("dragleave", onDragLeave);
  window.addEventListener("paste", onPaste);
  window.addEventListener("drop", onDropWindow);
  dropzoneElement.addEventListener("drop", onDrop);

  return {
    dispose: () => {
      window.removeEventListener("dragenter", onDragHover);
      window.removeEventListener("dragover", onDragHover);
      window.removeEventListener("dragleave", onDragLeave);
      window.removeEventListener("paste", onPaste);
      window.removeEventListener("drop", onDropWindow);
      dropzoneElement.removeEventListener("drop", onDrop);
    },
  };
}
