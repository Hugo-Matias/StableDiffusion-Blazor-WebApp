// VideoInput.js - Drag/drop + browser-side preview + direct fetch upload.
//
// Why fetch instead of Blazor's InputFile?
//   InputFile streams bytes through the SignalR circuit. For multi-hundred-MB
//   videos this saturates the circuit, freezes the page, and trips the
//   server-side IO timeout. By POSTing FormData via fetch we bypass SignalR
//   entirely and the only thing that flows over the circuit is the resulting
//   filename string.

export function init(
  dropzoneEl,
  inputEl,
  videoEl,
  dotNetRef,
  uploadEndpoint,
  maxFileSizeMb,
) {
  return new VideoInputHandler(
    dropzoneEl,
    inputEl,
    videoEl,
    dotNetRef,
    uploadEndpoint,
    maxFileSizeMb,
  );
}

class VideoInputHandler {
  constructor(dropzone, input, video, dotNetRef, endpoint, maxFileSizeMb) {
    this.dropzone = dropzone;
    this.input = input;
    this.video = video;
    this.dotNetRef = dotNetRef;
    this.endpoint = endpoint || "/api/upload-source";
    this.maxBytes = (maxFileSizeMb || 1024) * 1024 * 1024;
    this.objectUrl = null;
    this.activeXhr = null;

    this._onDragEnter = this.onDragEnter.bind(this);
    this._onDragLeave = this.onDragLeave.bind(this);
    this._onDragOver = this.onDragOver.bind(this);
    this._onDrop = this.onDrop.bind(this);
    this._onChange = this.onChange.bind(this);

    this.attach();
  }

  attach() {
    if (!this.dropzone) {
      console.warn("VideoInput: dropzone is null");
      return;
    }
    this.dropzone.addEventListener("dragenter", this._onDragEnter);
    this.dropzone.addEventListener("dragleave", this._onDragLeave);
    this.dropzone.addEventListener("dragover", this._onDragOver);
    this.dropzone.addEventListener("drop", this._onDrop);
    if (this.input) {
      this.input.addEventListener("change", this._onChange);
    }
  }

  onDragEnter(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dotNetRef.invokeMethodAsync("OnDragEnter");
  }

  onDragLeave(e) {
    e.preventDefault();
    e.stopPropagation();
    if (!this.dropzone.contains(e.relatedTarget)) {
      this.dotNetRef.invokeMethodAsync("OnDragLeave");
    }
  }

  onDragOver(e) {
    e.preventDefault();
    e.stopPropagation();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
  }

  onDrop(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dotNetRef.invokeMethodAsync("OnDrop");
    const files = e.dataTransfer && e.dataTransfer.files;
    if (files && files.length > 0) {
      this.handleFile(files[0]);
    }
  }

  onChange(e) {
    const files = e.target && e.target.files;
    if (files && files.length > 0) {
      this.handleFile(files[0]);
    }
  }

  handleFile(file) {
    if (!file) return;
    if (!file.type || !file.type.startsWith("video/")) {
      this.dotNetRef.invokeMethodAsync(
        "OnUploadFailed",
        `Unsupported file type: ${file.type || "unknown"}`,
      );
      return;
    }
    if (file.size > this.maxBytes) {
      const limitMb = Math.round(this.maxBytes / (1024 * 1024));
      this.dotNetRef.invokeMethodAsync(
        "OnUploadFailed",
        `File exceeds ${limitMb} MB limit.`,
      );
      return;
    }

    // Browser-side preview via object URL - no bytes leave the browser yet.
    this.setPreview(file);

    // Tell .NET we're starting so it shows the spinner.
    this.dotNetRef.invokeMethodAsync("OnUploadStarted");

    // Upload via XHR so we get progress events. fetch() doesn't expose upload progress.
    const form = new FormData();
    form.append("file", file, file.name);

    const xhr = new XMLHttpRequest();
    this.activeXhr = xhr;
    xhr.open("POST", this.endpoint, true);
    xhr.responseType = "json";
    xhr.upload.onprogress = (ev) => {
      if (ev.lengthComputable) {
        const pct = Math.round((ev.loaded / ev.total) * 100);
        this.dotNetRef.invokeMethodAsync("OnUploadProgress", pct);
      }
    };
    xhr.onload = () => {
      this.activeXhr = null;
      if (xhr.status >= 200 && xhr.status < 300) {
        const resp = xhr.response || {};
        const filename = resp.filename || "";
        const meta = this.readVideoMetadata();
        this.dotNetRef.invokeMethodAsync(
          "OnUploadCompleted",
          filename,
          meta.width,
          meta.height,
          meta.duration,
        );
      } else {
        const msg =
          (xhr.response && xhr.response.error) ||
          `Upload failed (HTTP ${xhr.status}).`;
        this.dotNetRef.invokeMethodAsync("OnUploadFailed", msg);
      }
    };
    xhr.onerror = () => {
      this.activeXhr = null;
      this.dotNetRef.invokeMethodAsync(
        "OnUploadFailed",
        "Network error during upload.",
      );
    };
    xhr.onabort = () => {
      this.activeXhr = null;
    };
    xhr.send(form);
  }

  setPreview(file) {
    if (!this.video) return;
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
    this.objectUrl = URL.createObjectURL(file);
    try {
      this.video.src = this.objectUrl;
      this.video.classList.remove("hidden");
      // Ensure metadata loads so we can grab dimensions/duration on completion.
      this.video.load();
    } catch (err) {
      console.warn("VideoInput: failed to set preview", err);
    }
  }

  readVideoMetadata() {
    const v = this.video;
    if (!v) return { width: 0, height: 0, duration: 0 };
    const w = v.videoWidth || 0;
    const h = v.videoHeight || 0;
    const d = isFinite(v.duration) ? v.duration : 0;
    return { width: w, height: h, duration: d };
  }

  openPicker() {
    if (this.input) {
      try {
        this.input.click();
      } catch (err) {
        console.warn("VideoInput: openPicker failed", err);
      }
    }
  }

  clear() {
    if (this.activeXhr) {
      try {
        this.activeXhr.abort();
      } catch {
        /* ignored */
      }
      this.activeXhr = null;
    }
    if (this.video) {
      try {
        this.video.pause();
        this.video.removeAttribute("src");
        this.video.load();
        this.video.classList.add("hidden");
      } catch {
        /* ignored */
      }
    }
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
    if (this.input) {
      try {
        this.input.value = "";
      } catch {
        /* ignored */
      }
    }
  }

  dispose() {
    if (this.activeXhr) {
      try {
        this.activeXhr.abort();
      } catch {
        /* ignored */
      }
      this.activeXhr = null;
    }
    if (this.dropzone) {
      this.dropzone.removeEventListener("dragenter", this._onDragEnter);
      this.dropzone.removeEventListener("dragleave", this._onDragLeave);
      this.dropzone.removeEventListener("dragover", this._onDragOver);
      this.dropzone.removeEventListener("drop", this._onDrop);
    }
    if (this.input) {
      this.input.removeEventListener("change", this._onChange);
    }
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
