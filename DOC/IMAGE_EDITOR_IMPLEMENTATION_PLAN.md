# Image Editor Implementation Plan

> **Document Version:** 4.0  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 3 Complete, Planning Phase 4+  
> **Target:** Img2ImgComfyUI Page Enhancement

---

## Executive Summary

The Image Editor is a Fabric.js-based drawing and masking tool integrated into the Blazor WebApp. It enables users to edit images before sending them to ComfyUI for img2img generation, and to create inpainting masks for targeted image modifications.

**Primary Focus**: Inpainting and outpainting workflows for AI image generation.

---

## Phase Overview

| Phase | Description | Status | Priority |
|-------|-------------|--------|----------|
| **Phase 1** | Core Canvas Infrastructure | ✅ Complete | P0 |
| **Phase 2** | Drawing & Mask Tools | ✅ Complete | P0 |
| **Phase 3** | Layer System & Compositing | ✅ Complete | P1 |
| **Phase 4** | Mask Optimization | 🔴 Not Started | P0 |
| **Phase 5** | Selection Tools | 🔴 Not Started | P1 |
| **Phase 6** | Outpainting Support | 🔴 Not Started | P1 |
| **Phase 7** | Additional Tools | 🔴 Not Started | P2 |
| **Phase 8** | Polish & Enhancements | 🔴 Not Started | P3 |

---

## Completed Phases Summary

### Phase 1: Core Canvas Infrastructure ✅
- Fabric.js integration with Blazor
- Canvas zoom/pan with mouse wheel and space+drag
- Image loading and display
- Viewport management
- Basic state management

### Phase 2: Drawing & Mask Tools ✅
- Brush tool with customizable size/color
- Eraser tool (stroke-level)
- Mask brush and mask eraser
- Color picker tool
- Undo/redo system

### Phase 3: Layer System & Compositing ✅
- Layer panel UI with visibility/lock/opacity controls
- Layer reordering
- Image import (drag-drop, paste, file picker)
- Object selection and transformation
- Copy/paste/duplicate/flip operations
- State preservation (Save vs Apply workflow)

---

## Phase 4: Mask Optimization 🔴 NOT STARTED

### Objectives
Fix mask UX issues to provide professional inpainting experience.

### Current Issues
1. **Opacity stacking**: Overlapping mask strokes create darker areas
2. **No visual distinction**: Mask doesn't clearly indicate "mask mode"
3. **Export format**: Needs guaranteed binary black/white output

### Planned Deliverables

#### 4.1 Flat Mask Rendering
| Item | Description | Status |
|------|-------------|--------|
| Offscreen mask canvas | Composite all mask strokes to single canvas | 🔴 |
| Union rendering | Any painted pixel = fully masked (no opacity stacking) | 🔴 |
| Real-time preview | Update mask overlay after each stroke | 🔴 |

**Technical Approach**:
```
┌─────────────────────────────────────┐
│  Mask Stroke Objects (Fabric.js)   │
│  - Individual vector paths         │
│  - Used for undo/redo              │
│  - Hidden from direct display      │
└─────────────────────────────────────┘
                 ↓ (composite on stroke end)
┌─────────────────────────────────────┐
│  Offscreen Canvas (mask-buffer)    │
│  - Renders all strokes as white    │
│  - Full opacity (no stacking)      │
│  - globalCompositeOperation: src   │
└─────────────────────────────────────┘
                 ↓ (display)
┌─────────────────────────────────────┐
│  Mask Display Layer                 │
│  - User-configurable color         │
│  - User-configurable opacity       │
│  - CSS animation overlay           │
└─────────────────────────────────────┘
```

#### 4.2 Binary Mask Export
| Item | Description | Status |
|------|-------------|--------|
| Black/white output | Export mask as pure B/W PNG | 🔴 |
| Same dimensions | Match base image dimensions | 🔴 |
| No anti-aliasing artifacts | Clean edges for AI processing | 🔴 |

#### 4.3 Animated Mask Overlay
| Item | Description | Status |
|------|-------------|--------|
| CSS stripe animation | Animated diagonal lines over masked areas | 🔴 |
| CSS mask-image clipping | Pattern only shows over mask pixels | 🔴 |
| Fallback to glow | If CSS animation too complex, use pulsing border | 🔴 |

**Primary CSS Implementation**:
```css
.mask-pattern-overlay {
    position: absolute;
    pointer-events: none;
    mix-blend-mode: overlay;
    opacity: 0.3;
    background: repeating-linear-gradient(
        -45deg,
        transparent 0px,
        transparent 4px,
        rgba(255,255,255,0.5) 4px,
        rgba(255,255,255,0.5) 8px
    );
    animation: stripe-move 1s linear infinite;
}

@keyframes stripe-move {
    from { background-position: 0 0; }
    to { background-position: 11.3px 0; }
}
```

**Mask clipping**:
```javascript
// Clip pattern to mask shape
patternOverlay.style.maskImage = `url(${maskCanvas.toDataURL()})`;
patternOverlay.style.webkitMaskImage = `url(${maskCanvas.toDataURL()})`;
```

#### 4.4 Mask Color Picker
| Item | Description | Status |
|------|-------------|--------|
| Reuse brush color picker | Same UI, context-aware (brush vs mask) | 🔴 |
| Separate mask color state | Store in `_state.MaskOverlayColor` | 🔴 |
| Update display on change | Re-render mask overlay with new color | 🔴 |

#### Already Implemented
| Item | Status |
|------|--------|
| Mask visibility toggle | ✅ Done |
| Mask opacity slider | ✅ Done |

### Implementation Notes
- Keep vector strokes for undo/redo capability
- Only composite to offscreen canvas for display and export
- Mask eraser should erase from the composited result conceptually, but still work on strokes

---

## Phase 5: Selection Tools 🔴 NOT STARTED

### Objectives
Enable precise area selection for mask creation and editing.

### Planned Deliverables

#### 5.1 Selection Tools
| Item | Description | Priority |
|------|-------------|----------|
| Rectangular selection | Click-drag rectangle | High |
| Elliptical selection | Click-drag ellipse | High |
| Lasso selection | Freeform polygon/path | High |
| Clear selection | Deselect all (Escape key) | High |
| Invert selection | Flip selected/unselected | High |

#### 5.2 Selection to Mask
| Item | Description | Priority |
|------|-------------|----------|
| Convert to mask | Fill selection area as mask | Critical |
| Additive mode | Add to existing mask (not replace) | High |
| Selection visualization | Marching ants or highlight | Medium |

**Workflow**:
1. User draws selection (rect/ellipse/lasso)
2. Click "Selection to Mask" button or keyboard shortcut
3. Selection area is added to existing mask
4. User can refine with mask brush/eraser

#### 5.3 Deferred
| Item | Status | Notes |
|------|--------|-------|
| Magic wand | Deferred | Complex, results may be unsatisfactory |

---

## Phase 6: Outpainting Support 🔴 NOT STARTED

### Objectives
Enable canvas extension beyond original image boundaries for outpainting workflows.

### Planned Deliverables

#### 6.1 Canvas Extension
| Item | Description | Priority |
|------|-------------|----------|
| Canvas resize | Extend canvas in any direction | Critical |
| Anchor options | Position original image (center, corners, edges) | High |
| Fill options | Transparent, white, black, or custom color | High |

#### 6.2 Outpaint Mask Generation
| Item | Description | Priority |
|------|-------------|----------|
| Auto-mask extended areas | New canvas areas automatically masked | High |
| Combined with inpaint mask | Single mask output for workflow | Medium |

#### 6.3 Presets & UI
| Item | Description | Priority |
|------|-------------|----------|
| Aspect ratio presets | 16:9, 4:3, 1:1, etc. | Medium |
| Directional extension | Add Npx to specific side | Medium |
| Target resolution mode | Extend to specific dimensions | Low |

### Open Questions (Pending ComfyUI Integration)
> These questions will be resolved when implementing the ComfyUI outpainting workflow:

1. **Mask handling**: Should outpaint mask be separate from inpaint mask, or combined into single output?
2. **Extension mode**: Prefer directional (add 256px to right) or target resolution (extend to 1920x1080)?
3. **Auto-mask behavior**: Should extended areas automatically become masked, or require user confirmation?
4. **Workflow integration**: How does the outpainting workflow differ from inpainting in ComfyUI nodes?

---

## Phase 7: Additional Tools 🔴 NOT STARTED

### Objectives
Complete the editing toolkit with commonly needed features.

### Planned Deliverables

| Item | Description | Priority |
|------|-------------|----------|
| Crop tool | Crop canvas to selection or custom area | Medium |
| Flatten layer | Convert vector layer to raster (enables pixel erasing) | Medium |
| Pixel-level eraser | Requires rasterized layer | Low |

### Implementation Notes
- Crop tool should offer aspect ratio constraints
- Flatten layer is prerequisite for true pixel-level operations
- Consider "Flatten visible" vs "Flatten selected layer"

---

## Phase 8: Polish & Enhancements 🔴 NOT STARTED

### Objectives
Nice-to-have features for future consideration.

### Potential Deliverables

| Item | Description | Priority |
|------|-------------|----------|
| Layer blending modes | Multiply, Screen, Overlay, etc. | Low |
| Image adjustments | Brightness, Contrast, Blur | Low |
| Shapes & Text | Rectangle, Ellipse, Line, Text tools | Low |
| Magic wand selection | Color-based selection | Low |
| Touch/mobile support | Touch gestures for tablet use | Low |
| Keyboard shortcuts help | Modal showing all shortcuts | Low |

---

## Design Decisions Log

### Mask System
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Mask overlay opacity stacking | Fix with offscreen compositing | Better UX, matches user expectations |
| Mask visual feedback | Animated stripe pattern (CSS) | Clear indication of mask mode, low performance impact |
| Mask color | Customizable via color picker | Reuses existing UI, flexible for different images |
| Selection to mask mode | Additive | User can clear mask if they want to replace |
| Mask export format | Binary B/W PNG | Required for AI inpainting workflows |

### Layer System
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Raster vs Vector | Vector (Fabric.js paths) | Enables undo/redo, scales well |
| Pixel-level erasing | Deferred to Phase 7 | Requires rasterization, complex |
| Layer blending | Deferred to Phase 8 | Normal blending sufficient for now |

### Outpainting
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Implementation timing | Phase 6 | Depends on ComfyUI workflow research |
| Questions preserved | In document | Will resolve during implementation |

---

## File Reference

### Core Files
| File | Purpose |
|------|---------|
| `Components/ImageEditor/ImageEditorModal.razor` | Main Blazor component |
| `Components/ImageEditor/ImageEditorModal.razor.css` | Scoped CSS styles |
| `Components/ImageEditor/LayerPanel.razor` | Layer management sidebar |
| `Components/ImageEditor/LayerPanel.razor.css` | Layer panel styles |
| `wwwroot/js/ImageEditor/ImageEditor.js` | Main entry point |
| `wwwroot/js/ImageEditor/ImageEditor.layers.js` | Layer management |
| `wwwroot/js/ImageEditor/ImageEditor.canvas.js` | Canvas operations |
| `wwwroot/js/ImageEditor/ImageEditor.tools.js` | Tool handling |
| `wwwroot/js/ImageEditor/ImageEditor.history.js` | Undo/redo |
| `wwwroot/js/ImageEditor/ImageEditor.export.js` | Export/import |
| `wwwroot/js/ImageEditor/ImageEditor.events.js` | Event handlers |
| `wwwroot/js/ImageEditor/ImageEditor.callbacks.js` | .NET interop |
| `wwwroot/js/ImageEditor/ImageEditor.utils.js` | Utilities |
| `Models/ImageEditorState.cs` | C# state model |
| `Services/ManagerService.cs` | State storage |

---

## Changelog

### Version 4.0 (December 2024) - Phase Planning Update

#### Document Restructure
- Added Phases 4-8 with detailed planning
- Moved completed phases to summary section
- Added Design Decisions Log
- Added Open Questions for outpainting

#### Phase 4: Mask Optimization (Planned)
- Flat mask rendering with offscreen canvas compositing
- Binary B/W mask export
- Animated CSS stripe overlay for mask visualization
- Mask color picker (reusing brush color UI)

#### Phase 5: Selection Tools (Planned)
- Rectangular, Elliptical, Lasso selection
- Selection to Mask conversion (additive mode)
- Invert selection
- Magic wand deferred

#### Phase 6: Outpainting Support (Planned)
- Canvas extension with anchor options
- Auto-mask for extended areas
- Open questions documented for ComfyUI integration

#### Phase 7: Additional Tools (Planned)
- Crop tool
- Flatten layer / rasterization
- Pixel-level eraser (post-rasterization)

#### Phase 8: Polish (Planned)
- Blending modes, adjustments, shapes, text
- Touch support, help modal

### Version 3.7 (December 2024) - Phase 3 Complete
- Object management: Copy/Paste/Duplicate/Flip
- Fabric.js 6.x async clone compatibility
- Toolbar buttons for object actions

### Previous Versions
See git history for earlier changelog entries.

---

## Implementation Checklist

### Phase 4: Mask Optimization
- [ ] Create offscreen canvas for mask compositing
- [ ] Render mask strokes as union (no opacity stacking)
- [ ] Update mask display after each stroke
- [ ] Implement CSS stripe animation overlay
- [ ] Add CSS mask-image clipping to pattern
- [ ] Implement fallback glow effect if needed
- [ ] Add mask color to color picker (context-aware)
- [ ] Ensure binary B/W export
- [ ] Test with various mask stroke patterns

### Phase 5: Selection Tools
- [ ] Add selection tool to toolbar
- [ ] Implement rectangular selection
- [ ] Implement elliptical selection
- [ ] Implement lasso/freeform selection
- [ ] Add selection visualization (marching ants or highlight)
- [ ] Implement "Selection to Mask" action
- [ ] Implement "Invert Selection" action
- [ ] Implement "Clear Selection" (Escape key)
- [ ] Test selection + mask workflow

### Phase 6: Outpainting
- [ ] Research ComfyUI outpainting workflow
- [ ] Resolve open questions
- [ ] Implement canvas resize with anchor
- [ ] Implement fill options for extended areas
- [ ] Implement auto-mask for extended areas
- [ ] Add UI for outpaint controls
- [ ] Test with ComfyUI workflow

### Phase 7: Additional Tools
- [ ] Implement crop tool
- [ ] Implement layer flattening
- [ ] Implement pixel-level eraser (on raster layers)

### Phase 8: Polish
- [ ] Layer blending modes
- [ ] Image adjustments
- [ ] Shapes and text
- [ ] Touch support
- [ ] Keyboard shortcuts help
