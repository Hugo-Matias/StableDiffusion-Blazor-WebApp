// AudioInput.js - Drag/drop + direct XHR upload for audio reference files.
//
// Mirrors VideoInput.js: uploads via XHR to /api/upload-source so the bytes
// never travel through the SignalR circuit. This prevents the
// "Cannot send data if the connection is not in the 'Connected' State" error
// that occurs when large base64 payloads saturate the circuit.

export function init(
  dropzoneEl,
  inputEl,
  audioEl,
  dotNetRef,
  uploadEndpoint,
  maxFileSizeMb,
) {
  return new AudioInputHandler(
    dropzoneEl,
    inputEl,
    audioEl,
    dotNetRef,
    uploadEndpoint,
    maxFileSizeMb,
  );
}

class AudioInputHandler {
  constructor(dropzone, input, audio, dotNetRef, endpoint, maxFileSizeMb) {
    this.dropzone = dropzone;
    this.input = input;
    this.audio = audio;
    this.dotNetRef = dotNetRef;
    this.endpoint = endpoint || "/api/upload-source";
    this.maxBytes = (maxFileSizeMb || 200) * 1024 * 1024;
    this.objectUrl = null;
    this.activeXhr = null;

    this._onDragEnter = this.onDragEnter.bind(this);
    this._onDragLeave = this.onDragLeave.bind(this);
    this._onDragOver = this.onDragOver.bind(this);
    this._onDrop = this.onDrop.bind(this);
    this._onChange = this.onChange.bind(this);
    this._onLoadedMetadata = this.onLoadedMetadata.bind(this);

    this.attach();
  }

  attach() {
    if (!this.dropzone) return;
    this.dropzone.addEventListener("dragenter", this._onDragEnter);
    this.dropzone.addEventListener("dragleave", this._onDragLeave);
    this.dropzone.addEventListener("dragover", this._onDragOver);
    this.dropzone.addEventListener("drop", this._onDrop);

    if (this.input) {
      this.input.addEventListener("change", this._onChange);
    }

    if (this.audio) {
      this.audio.addEventListener("loadedmetadata", this._onLoadedMetadata);
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
    if (this.dropzone && !this.dropzone.contains(e.relatedTarget)) {
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
    // Reset so the same file can trigger 'change' again.
    e.target.value = "";
  }

  onLoadedMetadata() {
    if (!this.audio) return;
    const duration = this.audio.duration || 0;
    this.dotNetRef.invokeMethodAsync("OnAudioMetadataChanged", 0, 0, duration);
  }

  handleFile(file) {
    if (!file) return;

    const isAudioType = file.type && file.type.startsWith("audio/");
    const ext = (file.name.split(".").pop() || "").toLowerCase();
    const audioExts = [
      "wav",
      "mp3",
      "flac",
      "ogg",
      "m4a",
      "aac",
      "wma",
      "opus",
      "webm",
    ];
    const isAudioExt = audioExts.includes(ext);

    if (!isAudioType && !isAudioExt) {
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

    // Read duration from local file before uploading so we can show it immediately.
    const tempAudio = new Audio();
    this.objectUrl = URL.createObjectURL(file);
    tempAudio.src = this.objectUrl;
    tempAudio.addEventListener("loadedmetadata", () => {
      const duration = tempAudio.duration || 0;
      // Set local preview on the real audio element.
      if (this.audio) {
        this.audio.src = this.objectUrl || "";
      }
      this.dotNetRef.invokeMethodAsync(
        "OnAudioMetadataChanged",
        0,
        0,
        duration,
      );
    });

    // Tell .NET we're starting so it shows the spinner.
    this.dotNetRef.invokeMethodAsync("OnUploadStarted");

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
        // Switch audio element to server-side preview URL.
        if (this.audio && this.objectUrl) {
          URL.revokeObjectURL(this.objectUrl);
          this.objectUrl = null;
        }
        this.dotNetRef.invokeMethodAsync(
          "OnUploadCompleted",
          filename,
          0,
          0,
          tempAudio.duration || 0,
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

  setSrc(src) {
    if (!this.audio) return;
    // Revoke previous object URL to free memory.
    if (this.objectUrl) {
      try {
        URL.revokeObjectURL(this.objectUrl);
      } catch {
        /* ignored */
      }
      this.objectUrl = null;
    }
    this.audio.src = src || "";
    if (src) {
      this.audio.load();
    }
  }

  setTrim(trimStart, trimDuration) {
    // Seek audio preview to match trim settings - same logic as VideoInput frame preview.
    if (!this.audio) return;
    const start = Number(trimStart) || 0;
    this.audio.dataset.trimStart = String(start);
    this.audio.dataset.trimDuration = String(trimDuration || "");

    // Remove old loop listener by cloning the element (simplest way to detach).
    if (start > 0 || trimDuration) {
      const end = trimDuration ? start + Number(trimDuration) : Infinity;
      this._trimEnd = isFinite(end) ? end : this.audio.duration || Infinity;

      // Seek to trim start immediately.
      try {
        this.audio.currentTime = start;
      } catch {
        /* not loaded yet */
      }

      // Re-attach timeupdate listener for loop boundary.
      if (!this._onTimeUpdate) {
        this._onTimeUpdate = (e) => {
          const t = e.target.currentTime || 0;
          if (t >= this._trimEnd && isFinite(this._trimEnd)) {
            e.target.currentTime = start;
          }
        };
      }
      try {
        // Remove old listener first to avoid duplicates.
        this.audio.removeEventListener("timeupdate", this._onTimeUpdate);
        this.audio.addEventListener("timeupdate", this._onTimeUpdate);
      } catch {
        /* ignored */
      }

      // Handle native loop/ended event so playback loops back to trim start, not 0.
      if (!this._onEnded) {
        this._onEnded = (e) => {
          e.target.currentTime = start;
          e.target.play().catch(() => {
            /* autoplay may be blocked */
          });
        };
      }
      try {
        this.audio.removeEventListener("ended", this._onEnded);
        this.audio.addEventListener("ended", this._onEnded);
      } catch {
        /* ignored */
      }
    } else {
      // No trim constraints - remove loop listener.
      if (this._onTimeUpdate) {
        try {
          this.audio.removeEventListener("timeupdate", this._onTimeUpdate);
        } catch {
          /* ignored */
        }
      }
      if (this._onEnded) {
        try {
          this.audio.removeEventListener("ended", this._onEnded);
        } catch {
          /* ignored */
        }
      }
      try {
        this.audio.currentTime = 0;
      } catch {
        /* not loaded yet */
      }
    }
  }

  openPicker() {
    if (this.input) {
      this.input.click();
    }
  }

  clear() {
    // Remove trim loop listener.
    if (this.audio && this._onTimeUpdate) {
      try {
        this.audio.removeEventListener("timeupdate", this._onTimeUpdate);
      } catch {
        /* ignored */
      }
    }
    // Remove ended/loop listener.
    if (this.audio && this._onEnded) {
      try {
        this.audio.removeEventListener("ended", this._onEnded);
      } catch {
        /* ignored */
      }
    }
    this._trimEnd = Infinity;

    if (this.audio) {
      this.audio.src = "";
    }
    if (this.objectUrl) {
      try {
        URL.revokeObjectURL(this.objectUrl);
      } catch {
        /* ignored */
      }
      this.objectUrl = null;
    }
  }

  dispose() {
    if (this.dropzone) {
      try {
        this.dropzone.removeEventListener("dragenter", this._onDragEnter);
      } catch {
        /* ignored */
      }
      try {
        this.dropzone.removeEventListener("dragleave", this._onDragLeave);
      } catch {
        /* ignored */
      }
      try {
        this.dropzone.removeEventListener("dragover", this._onDragOver);
      } catch {
        /* ignored */
      }
      try {
        this.dropzone.removeEventListener("drop", this._onDrop);
      } catch {
        /* ignored */
      }
    }
    if (this.input) {
      try {
        this.input.removeEventListener("change", this._onChange);
      } catch {
        /* ignored */
      }
    }
    if (this.audio) {
      try {
        this.audio.removeEventListener(
          "loadedmetadata",
          this._onLoadedMetadata,
        );
      } catch {
        /* ignored */
      }
    }
    if (this.activeXhr) {
      try {
        this.activeXhr.abort();
      } catch {
        /* ignored */
      }
      this.activeXhr = null;
    }
    this.clear();
  }
}
