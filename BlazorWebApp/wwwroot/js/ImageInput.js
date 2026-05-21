// ImageInput.js - Handles drag/drop and paste interactions only
// Click handling is done by the native InputFile component

export function init(dropzoneElement, inputElement, dotNetRef) {
  const instance = new ImageInputHandler(
    dropzoneElement,
    inputElement,
    dotNetRef,
  );
  return instance;
}

class ImageInputHandler {
  constructor(dropzoneElement, inputElement, dotNetRef) {
    this.dropzone = dropzoneElement;
    this.input = inputElement;
    this.dotNetRef = dotNetRef;

    // Bind handlers for proper cleanup
    this._handleDragEnter = this.handleDragEnter.bind(this);
    this._handleDragLeave = this.handleDragLeave.bind(this);
    this._handleDragOver = this.handleDragOver.bind(this);
    this._handleDrop = this.handleDrop.bind(this);
    this._handlePaste = this.handlePaste.bind(this);

    this.setupEventListeners();
  }

  setupEventListeners() {
    if (!this.dropzone) {
      console.warn("ImageInputHandler: dropzone element is null");
      return;
    }

    // Drag and drop events only
    this.dropzone.addEventListener("dragenter", this._handleDragEnter);
    this.dropzone.addEventListener("dragleave", this._handleDragLeave);
    this.dropzone.addEventListener("dragover", this._handleDragOver);
    this.dropzone.addEventListener("drop", this._handleDrop);

    // Paste event (global)
    document.addEventListener("paste", this._handlePaste);

    // NO click handler - the InputFile component handles clicks natively
  }

  handleDragEnter(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dotNetRef.invokeMethodAsync("OnDragEnter");
  }

  handleDragLeave(e) {
    e.preventDefault();
    e.stopPropagation();
    // Only trigger if we're leaving the dropzone entirely
    if (!this.dropzone.contains(e.relatedTarget)) {
      this.dotNetRef.invokeMethodAsync("OnDragLeave");
    }
  }

  handleDragOver(e) {
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = "copy";
  }

  handleDrop(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dotNetRef.invokeMethodAsync("OnDrop");

    const files = e.dataTransfer?.files;
    if (files && files.length > 0 && files[0].type.startsWith("image/")) {
      this.loadImageFile(files[0]);
    }
  }

  handlePaste(e) {
    const items = e.clipboardData?.items;
    if (!items) return;

    for (const item of items) {
      if (item.type.startsWith("image/")) {
        e.preventDefault();
        const file = item.getAsFile();
        if (file) {
          this.loadImageFile(file);
        }
        break;
      }
    }
  }

  async pasteFromClipboard() {
    try {
      const clipboardItems = await navigator.clipboard.read();
      for (const item of clipboardItems) {
        for (const type of item.types) {
          if (type.startsWith("image/")) {
            const blob = await item.getType(type);
            this.loadImageFile(blob);
            return;
          }
        }
      }
    } catch (err) {
      console.warn("Failed to read clipboard:", err);
    }
  }

  loadImageFile(file) {
    const reader = new FileReader();
    reader.onload = (e) => {
      const imageData = e.target.result;
      this.dotNetRef.invokeMethodAsync("OnImagePasted", imageData);
    };
    reader.onerror = (e) => {
      console.error("Failed to read file:", e);
    };
    reader.readAsDataURL(file);
  }

  async createBlackoutMaskOverlay(imageSrc, maskSrc, blackoutOpacity = 0.72) {
    if (!imageSrc || !maskSrc) {
      return null;
    }

    const [image, mask] = await Promise.all([
      this.loadImageElement(imageSrc),
      this.loadImageElement(maskSrc),
    ]);

    const width = image.naturalWidth || image.width;
    const height = image.naturalHeight || image.height;
    if (!width || !height) {
      return null;
    }

    const output = document.createElement("canvas");
    output.width = width;
    output.height = height;
    const outputCtx = output.getContext("2d");
    outputCtx.drawImage(image, 0, 0, width, height);

    const maskCanvas = document.createElement("canvas");
    maskCanvas.width = width;
    maskCanvas.height = height;
    const maskCtx = maskCanvas.getContext("2d");
    maskCtx.drawImage(mask, 0, 0, width, height);
    const maskPixels = maskCtx.getImageData(0, 0, width, height).data;

    const blackout = outputCtx.createImageData(width, height);
    for (let i = 0; i < maskPixels.length; i += 4) {
      const luminance =
        (maskPixels[i] * 0.2126 +
          maskPixels[i + 1] * 0.7152 +
          maskPixels[i + 2] * 0.0722) /
        255;
      const alpha = Math.round(255 * blackoutOpacity * (1 - luminance));
      blackout.data[i] = 0;
      blackout.data[i + 1] = 0;
      blackout.data[i + 2] = 0;
      blackout.data[i + 3] = alpha;
    }

    const blackoutCanvas = document.createElement("canvas");
    blackoutCanvas.width = width;
    blackoutCanvas.height = height;
    blackoutCanvas.getContext("2d").putImageData(blackout, 0, 0);
    outputCtx.drawImage(blackoutCanvas, 0, 0);

    return output.toDataURL("image/png");
  }

  loadImageElement(src) {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = reject;
      image.src = src;
    });
  }

  dispose() {
    if (this.dropzone) {
      this.dropzone.removeEventListener("dragenter", this._handleDragEnter);
      this.dropzone.removeEventListener("dragleave", this._handleDragLeave);
      this.dropzone.removeEventListener("dragover", this._handleDragOver);
      this.dropzone.removeEventListener("drop", this._handleDrop);
    }
    document.removeEventListener("paste", this._handlePaste);
  }
}
