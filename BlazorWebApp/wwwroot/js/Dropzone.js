export function init(dropzoneElement, inputFile) {
  function onDragHover(e) {
    e.preventDefault();
    if (!dropzoneElement.classList.contains("hover"))
      dropzoneElement.classList.add("hover");
  }

  function onDragLeave(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");
  }

  async function urlToObject(image) {
    const response = await fetch(image);
    const blob = await response.blob();
    const file = new File([blob], "image.png", { type: blob.type });
    return file;
  }

  async function onDrop(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");

    if (e.dataTransfer.files.length > 0) {
      inputFile.files = e.dataTransfer.files;
    } else {
      let data = e.dataTransfer.getData("Text");
      let file = await urlToObject(data);
      let fileList = new DataTransfer();
      fileList.items.add(file);
      inputFile.files = fileList.files;
    }

    const event = new Event("change", { bubbles: true });
    inputFile.dispatchEvent(event);
  }

  function onPaste(e) {
    inputFile.files = e.clipboardData.files;
    console.log(e.clipboardData.files);
    const event = new Event("change", { bubbles: true });
    inputFile.dispatchEvent(event);
  }

  function onDropWindow(e) {
    e.preventDefault();
    dropzoneElement.classList.remove("hover");
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
