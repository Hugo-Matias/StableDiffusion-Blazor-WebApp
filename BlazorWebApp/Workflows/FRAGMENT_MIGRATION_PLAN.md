## **Phase 2: Missing #meta Blocks** ? COMPLETE

**Priority:** ?? **HIGH** - Lacks proper metadata

| Fragment | Type | Action Required | Status |
|----------|------|-----------------|--------|
| `detailer.sbn` | ~~Core~~ Deprecated | ~~Analyze usage~~ Deleted | ? N/A - Removed |
| `save.sbn` | Core (always active) | Add #meta with outputs | ? Fixed |
| `save-video.sbn` | Core (always active) | Add #meta + consolidate | ? Fixed & Renamed |
| ~~`video-save.sbn`~~ | Duplicate | Consolidate & rename | ? Consolidated |

**Completed Actions:**
1. ? Deleted deprecated `detailer.sbn`
2. ? Added #meta blocks to all save fragments
3. ? Defined outputs for tracking
4. ? Added UI metadata for documentation
5. ? Updated `IsCoreFragment()` whitelist
6. ? No conditions needed (core fragments)
7. ? **Consolidated video save fragments** - Merged duplicates into `save-video.sbn`
8. ? **Renamed for consistency** - `video-save.sbn` ? `save-video.sbn` (matches `save.sbn` naming pattern)

**Fragment Consolidation & Rename:**
- **Old names:** `save-video.sbn` (legacy), `video-save.sbn` (newer)
- **New unified name:** `save-video.sbn` (follows `save.sbn` convention)
- **Rationale:** Groups with other save fragments alphabetically, easier to find

**Save Fragment Family:**
- `save.sbn` - Image output
- `save-video.sbn` - Video output

**Standardized `save-video.sbn`:**
- **Hardcoded:** `filename_prefix`, `format`, `pix_fmt`, `crf`, `save_metadata`, `save_output`
- **Configurable:** `frame_rate` (default 16), `image_input_name` (default "image_output"), `node_id` (default "video_save")

**Updated Templates:**
- ? `img2vid.sbn` - Uses `save-video.sbn`
- ? `pose2vid-steadydancer.sbn` - Uses `save-video.sbn` with standardized `image_input_name` parameter

**Hardcoded Values (save-video.sbn):**
- `filename_prefix`: `"tmp/vid"` (temporary ComfyUI output, cleaned on restart)
- `format`: `"video/h264-mp4"`
- `pix_fmt`: `"yuv420p"`
- `crf`: `19`
- `save_metadata`: `true`
- `save_output`: `true`

**Configurable Parameters:**
- `frame_rate`: Default 16, specified in templates
- `image_input_name`: Default `"image_output"`, can be overridden for frame interpolation or preview concat
- `node_id`: Default `"video_save"`, can be overridden for multiple saves

**Note:** Videos are saved to tmp folder then copied to proper output directory by ImageService after generation.

## **Phase 3: Scoped Fragments** ? COMPLETE (N/A - Core Fragments)

**Priority:** ?? **MEDIUM** - Initially thought to need conditional conditions

| Fragment | Analysis | Decision | Status |
|----------|----------|----------|--------|
| `empty-latent.sbn` | Always needed for resolution/latent creation | Core fragment | ? Added to whitelist |
| `load-diffusion.sbn` | Always needed to load models | Core fragment | ? Added to whitelist |
| `load-clip-vision.sbn` | Always needed in Wan workflows | Core fragment | ? Added to whitelist |

**Decision Rationale:**

These fragments use the `scope` parameter for **namespacing**, not for **conditional rendering**:

- **Scope purpose**: Prefix node IDs to avoid conflicts (e.g., `detailer_unet_loader` vs `unet_loader`)
- **Not optional**: Always required in their respective contexts
- **No conditions needed**: They are core infrastructure, not user-toggleable features

**Example:**
```json
// Main context
{
  "id": "loader",
  "fragment": "load-diffusion.sbn",
  "parameters": {} // No scope = main context
}

// Detailer context
{
  "id": "loader_detailer",
  "fragment": "load-diffusion.sbn",
  "parameters": {
    "scope": "detailer_" // Scoped for namespace
  }
}
```

**Both render** - the scope just changes the node IDs, it doesn't control activation.

**Action Taken:**
- ? Added fragments to `IsCoreFragment()` whitelist
- ? No conditional conditions needed
- ? Phase 3 complete (no code changes required)

**Note:** The scoped `load-diffusion-w-prompts.sbn` **does** need conditional conditions because it's used specifically for the detailer feature (already fixed in Phase 1).

## **Next Steps**

1. ? Complete Phase 1 testing
2. ? Complete Phase 2 (#meta blocks)
3. ? Complete Phase 3 (scoped fragments classified as core)
4. ?? **Continue Phase 4:** Categorize remaining 29 fragments

**Phase 4 Progress:** 3/29 fragments categorized (10%)
  - ? `load_image` ? Core
  - ? `load_video` ? Core
  - ? `lora_loader` ? Core

**Next Batch to Analyze:**
- [ ] Wan-specific fragments (loaders, samplers, encoders)
- [ ] Utility fragments (resize, get-image-size)
- [ ] Optional features (llm, pose-detection, model-sampling variants)

---

## **Phase 4: Detailed Analysis Required**

### **Wan Workflow Fragments** (Review for core vs optional)

| Fragment | Likely Category | Reason |
|----------|-----------------|--------|
| `load-wan-model.sbn` | Core (Wan) | Required for Wan workflows |
| `load-wan-vae.sbn` | Core (Wan) | Required for Wan workflows |
| `load-dual-models.sbn` | Core (specific) | Required for dual-model workflows |
| `sampler-wan.sbn` | Core (Wan) | Main sampler for Wan |
| `text-encode-wan.sbn` | Core (Wan) | Required for Wan text encoding |
| `decode-wan.sbn` | Core (Wan) | Required for Wan decoding |
| `i2v-encode.sbn` | Core (Wan) | Required for img2vid |
| `clip-vision.sbn` | Core (Wan) | Required for vision encoding |
| `context-options.sbn` | Core (Wan) | Required for context window |

**Next Action:** Verify these in Wan templates, add to whitelist

### **Utility Fragments** (Likely core in their contexts)

| Fragment | Likely Category | Reason |
|----------|-----------------|--------|
| `resize-image-kj.sbn` | Core | Always used for img2img resizing |
| `get-image-size.sbn` | Core | Utility for resolution detection |

### **Optional Feature Fragments** (Need `.IsActive` conditions)

| Fragment | Condition Needed | Reason |
|----------|------------------|--------|
| `llm.sbn` | `llm.IsActive` | Optional LLM enhancement feature |
| `pose-detection.sbn` | Always active in Wan | Actually core for pose2vid |
| `steadydancer-embeds.sbn` | Core (SteadyDancer) | Required for SteadyDancer workflow |
| `painter-i2v.sbn` | Core (Painter) | Required for Painter workflow |

### **Model Sampling Variants** (Need analysis)

| Fragment | Decision Needed |
|----------|-----------------|
| `model-sampling-auraflow.sbn` | Core for AuraFlow or optional? |
| `model-sampling-sd3.sbn` | Core for SD3 or optional? |
| `sampler-advanced.sbn` | Alternative to standard sampler? |
| `sampler-standard.sbn` | Core or alternative? |

**Recommendation:** Check which workflows use these, determine if they're workflow-specific (core) or user-toggleable (optional)

---

## **Updated Timeline Estimate**

| Phase | Status | Actual Time | Notes |
|-------|--------|-------------|-------|
| Phase 1 | ? Complete | 2 hours | As estimated |
| Phase 2 | ? Complete | 1 hour | Faster than estimated (simple changes) |
| Phase 3 | ? Complete | 0.5 hours | No changes needed (design decision) |
| Phase 4 | ?? In Progress | ~8-12 hours remaining | Categorization + testing |

**Estimated Completion:** 2-3 more sessions
