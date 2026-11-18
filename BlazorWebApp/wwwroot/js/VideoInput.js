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
    this.previewWindow = null;
    this.isProgrammaticSeek = false;
    this.videoFrameCallbackId = null;

    this._onDragEnter = this.onDragEnter.bind(this);
    this._onDragLeave = this.onDragLeave.bind(this);
    this._onDragOver = this.onDragOver.bind(this);
    this._onDrop = this.onDrop.bind(this);
    this._onChange = this.onChange.bind(this);
    this._onLoadedMetadata = this.onLoadedMetadata.bind(this);
    this._onTimeUpdate = this.onTimeUpdate.bind(this);
    this._onPlay = this.onPlay.bind(this);
    this._onSeeking = this.onSeeking.bind(this);
    this._onEnded = this.onEnded.bind(this);
    this._onVideoFrame = this.onVideoFrame.bind(this);

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
    if (this.video) {
      this.video.addEventListener("loadedmetadata", this._onLoadedMetadata);
      this.video.addEventListener("timeupdate", this._onTimeUpdate);
      this.video.addEventListener("play", this._onPlay);
      this.video.addEventListener("seeking", this._onSeeking);
      this.video.addEventListener("ended", this._onEnded);
    }
  }

  onLoadedMetadata() {
    const meta = this.readVideoMetadata();
    this.dotNetRef.invokeMethodAsync(
      "OnPreviewMetadataChanged",
      meta.width,
      meta.height,
      meta.duration,
    );
    this.enforcePreviewWindow(false);
    this.scheduleVideoFrameCallback();
  }

  onTimeUpdate() {
    this.enforcePreviewWindow(false);
  }

  onPlay() {
    this.enforcePreviewWindow(true);
    this.scheduleVideoFrameCallback();
  }

  onSeeking() {
    if (!this.isProgrammaticSeek) {
      this.enforcePreviewWindow(false);
    }
  }

  onEnded() {
    if (!this.previewWindow) return;

    this.seekTo(this.previewWindow.startTime || 0);
    this.video.play().catch(() => {});
  }

  onVideoFrame() {
    this.videoFrameCallbackId = null;
    this.enforcePreviewWindow(false);
    this.scheduleVideoFrameCallback();
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
      this.video.loop = false;
      this.video.src = this.objectUrl;
      this.video.classList.remove("hidden");
      // Ensure metadata loads so we can grab dimensions/duration on completion.
      this.video.load();
    } catch (err) {
      console.warn("VideoInput: failed to set preview", err);
    }
  }

  setRemotePreview(url) {
    if (!this.video) return;
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
    try {
      if (!url) {
        this.video.pause();
        this.video.removeAttribute("src");
        this.video.load();
        this.video.classList.add("hidden");
        this.previewWindow = null;
        this.cancelVideoFrameCallback();
        return;
      }

      this.video.loop = false;
      if (this.video.getAttribute("src") !== url) {
        this.video.src = url;
        this.video.load();
      }
      this.video.classList.remove("hidden");
    } catch (err) {
      console.warn("VideoInput: failed to set remote preview", err);
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

  setFramePreview(
    skipFirstFrames,
    selectEveryNth,
    frameLoadCap,
    forceRate,
    previewFrameOffset,
  ) {
    if (!this.video) return;

    const rate = Number(forceRate) || 0;
    if (rate <= 0) {
      this.previewWindow = null;
      if (this.video) {
        this.video.loop = true;
      }
      return;
    }

    const skip = Math.max(0, Number(skipFirstFrames) || 0);
    const every = Math.max(1, Number(selectEveryNth) || 1);
    const cap = Math.max(0, Number(frameLoadCap) || 0);
    const offsetLimit = cap > 0 ? cap - 1 : Number.MAX_SAFE_INTEGER;
    const offset = Math.max(
      0,
      Math.min(Number(previewFrameOffset) || 0, offsetLimit),
    );
    const startFrame = skip;
    const endFrameExclusive = cap > 0 ? skip + cap * every : null;
    const selectedFrame = skip + offset * every;
    const seekTime = selectedFrame / rate;

    this.previewWindow = {
      startFrame,
      endFrameExclusive,
      startTime: startFrame / rate,
      endTime: endFrameExclusive ? endFrameExclusive / rate : null,
      rate,
      every,
    };

    this.video.loop = false;

    const seek = () => {
      try {
        const duration = Number.isFinite(this.video.duration)
          ? this.video.duration
          : 0;
        this.seekTo(duration > 0 ? Math.min(seekTime, duration) : seekTime);
        this.enforcePreviewWindow(false);
        this.scheduleVideoFrameCallback();
      } catch (err) {
        console.warn("VideoInput: frame seek failed", err);
      }
    };

    if (this.video.readyState >= 1) {
      seek();
    } else {
      this.video.addEventListener("loadedmetadata", seek, { once: true });
    }
  }

  enforcePreviewWindow(resetBeforeStart) {
    if (!this.video || !this.previewWindow || this.isProgrammaticSeek) return;

    const window = this.previewWindow;
    const start = window.startTime || 0;
    const duration = Number.isFinite(this.video.duration)
      ? this.video.duration
      : 0;
    const end = window.endTime || duration || null;
    const current = this.video.currentTime || 0;
    const frameDuration = window.rate > 0 ? 1 / window.rate : 0.03;
    const epsilon = Math.max(0.03, frameDuration * 0.5);

    if (current < start - epsilon) {
      this.seekTo(start);
      return;
    }

    if (end && current >= end - epsilon) {
      this.seekTo(start);
      if (!this.video.paused) {
        this.video.play().catch(() => {});
      }
      return;
    }

    if (window.every <= 1 || window.rate <= 0 || this.video.paused) return;

    const currentFrame = Math.max(
      window.startFrame,
      Math.round(current * window.rate),
    );
    const distanceFromStart = currentFrame - window.startFrame;
    if (distanceFromStart % window.every === 0) return;

    let nextFrame =
      window.startFrame +
      Math.ceil(distanceFromStart / window.every) * window.every;
    if (window.endFrameExclusive && nextFrame >= window.endFrameExclusive) {
      nextFrame = window.startFrame;
    }

    const nextTime = nextFrame / window.rate;
    if (Math.abs(nextTime - current) > epsilon) {
      this.seekTo(nextTime);
    }
  }

  scheduleVideoFrameCallback() {
    if (
      !this.video ||
      !this.previewWindow ||
      this.video.paused ||
      this.videoFrameCallbackId !== null ||
      typeof this.video.requestVideoFrameCallback !== "function"
    ) {
      return;
    }

    this.videoFrameCallbackId = this.video.requestVideoFrameCallback(
      this._onVideoFrame,
    );
  }

  cancelVideoFrameCallback() {
    if (
      !this.video ||
      this.videoFrameCallbackId === null ||
      typeof this.video.cancelVideoFrameCallback !== "function"
    ) {
      this.videoFrameCallbackId = null;
      return;
    }

    this.video.cancelVideoFrameCallback(this.videoFrameCallbackId);
    this.videoFrameCallbackId = null;
  }

  seekTo(time) {
    if (!this.video) return;
    this.isProgrammaticSeek = true;
    try {
      this.video.currentTime = Math.max(0, time || 0);
    } finally {
      setTimeout(() => {
        this.isProgrammaticSeek = false;
      }, 0);
    }
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
        this.video.loop = false;
        this.video.removeAttribute("src");
        this.video.load();
        this.video.classList.add("hidden");
      } catch {
        /* ignored */
      }
    }
    this.previewWindow = null;
    this.cancelVideoFrameCallback();
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
    if (this.video) {
      this.video.removeEventListener("loadedmetadata", this._onLoadedMetadata);
      this.video.removeEventListener("timeupdate", this._onTimeUpdate);
      this.video.removeEventListener("play", this._onPlay);
      this.video.removeEventListener("seeking", this._onSeeking);
      this.video.removeEventListener("ended", this._onEnded);
    }
    this.cancelVideoFrameCallback();
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
