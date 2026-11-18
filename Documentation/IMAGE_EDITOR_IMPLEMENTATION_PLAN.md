# Image Editor Implementation Plan

> **Document Version:** 6.0  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 5 Complete, Development Paused  
> **Target:** Img2ImgComfyUI Page Enhancement

---

## Executive Summary

The Image Editor is a Fabric.js-based drawing and masking tool integrated into the Blazor WebApp. It enables users to edit images before sending them to ComfyUI for img2img generation, and to create inpainting masks for targeted image modifications.

**Primary Focus**: Inpainting and outpainting workflows for AI image generation.

**Current Status**: Core functionality complete through Phase 5. Development paused pending ComfyUI outpainting workflow research.

---

## Phase Overview

| Phase | Description | Status | Priority |
|-------|-------------|--------|----------|
| **Phase 1** | Core Canvas Infrastructure | ✅ Complete | P0 |
| **Phase 2** | Drawing & Mask Tools | ✅ Complete | P0 |
| **Phase 3** | Layer System & Compositing | ✅ Complete | P1 |
| **Phase 4** | Mask Optimization | ✅ Complete | P0 |
| **Phase 5** | Selection Tools | ✅ Complete | P1 |
| **Phase 6** | Outpainting Support | ⏸️ Pending Research | P1 |
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

### Phase 4: Mask Optimization ✅
- **Flat mask rendering**: Offscreen canvas compositing eliminates opacity stacking
- **Binary mask export**: Pure black/white PNG output for AI workflows
- **Visual overlay**: Colored mask with subtle diagonal stripe pattern
- **Multiple preview modes**: Overlay, Binary, Marching Ants, Blackout, Whiteout
- **Context-aware color picker**: Brush color vs mask color based on active tool
- **Mask brush color sync**: Brush stroke color matches display overlay color
- **State restoration**: Mask overlay properly refreshes on visibility toggle and session restore
- **Viewport sync**: Mask overlay follows zoom/pan transformations

**New File Created**: `wwwroot/js/ImageEditor/ImageEditor.mask.js`

### Phase 5: Selection Tools ✅
- **Rectangle selection**: Click-drag rectangle with marching ants border
- **Ellipse selection**: Click-drag ellipse with marching ants border
- **Lasso selection**: Freeform polygon/path selection
- **Selection modifiers**: Shift (constrain square/circle), Alt (draw from center)
- **Selection to mask**: Add selection to mask or subtract from mask
- **Invert selection**: Applies inverted area directly to mask
- **Clear selection**: Escape key or toolbar button
- **Toolbar cycling**: Click selection button to cycle through rect/ellipse/lasso when already active
- **Contextual UI**: Selection action buttons appear when selection exists

**New File Created**: `wwwroot/js/ImageEditor/ImageEditor.selection.js`

---

## Current Architecture

### File Structure
```
BlazorWebApp/
├── Components/ImageEditor/
│   ├── ImageEditorModal.razor          # Main Blazor component (UI, state, interop)
│   ├── ImageEditorModal.razor.css      # Scoped CSS styles
│   ├── LayerPanel.razor                # Layer management sidebar
│   └── LayerPanel.razor.css            # Layer panel styles
├── Models/
│   └── ImageEditorState.cs             # C# state model (tools, layers, history)
├── Services/
│   └── ManagerService.cs               # State storage between sessions
└── wwwroot/js/ImageEditor/
    ├── ImageEditor.js                  # Main entry point, mixin composition
    ├── ImageEditor.canvas.js           # Canvas operations, zoom/pan
    ├── ImageEditor.tools.js            # Tool handling, brush setup
    ├── ImageEditor.layers.js           # Layer management
    ├── ImageEditor.mask.js             # Mask compositing, preview modes
    ├── ImageEditor.selection.js        # Selection tools (rect/ellipse/lasso)
    ├── ImageEditor.history.js          # Undo/redo system
    ├── ImageEditor.export.js           # Image/mask export
    ├── ImageEditor.events.js           # Event handlers (mouse, keyboard)
    ├── ImageEditor.callbacks.js        # .NET interop callbacks
    └── ImageEditor.utils.js            # Utility functions
```

### Key Patterns

#### Mixin Architecture (JavaScript)
The editor uses a mixin pattern to compose functionality:
```javascript
// ImageEditor.js - Main class composes mixins
Object.assign(ImageEditor.prototype, CanvasMixin);
Object.assign(ImageEditor.prototype, ToolsMixin);
Object.assign(ImageEditor.prototype, LayersMixin);
Object.assign(ImageEditor.prototype, MaskMixin);
Object.assign(ImageEditor.prototype, SelectionMixin);
// etc.
```

#### State Management
- **C# State** (`ImageEditorState.cs`): UI state, tool settings, layer metadata
- **JS State**: Canvas state, Fabric.js objects, mask overlay
- **Persistence**: `ManagerService.ImageEditorState` preserves state between modal opens

#### Mask System
- Individual mask strokes stored as Fabric.js path objects (hidden)
- Offscreen canvas composites all strokes into flat mask
- HTML overlay element displays styled mask (positioned over canvas)
- Preview modes: Overlay (colored stripes), Binary (B/W), Marching Ants (dashed outline), Blackout, Whiteout

#### Selection System
- Selection shapes are Fabric.js Rect/Ellipse/Path objects with special styling
- Marching ants: Static dashed lines (animated version had performance issues)
- Selection-to-mask: Creates mask objects from selection geometry
- Auto-clear after mask operation

### Keyboard Shortcuts (Implemented)

| Shortcut | Action |
|----------|--------|
| B | Brush tool |
| E | Eraser tool |
| I | Color picker |
| V | Select/Move tool |
| M | Mask brush |
| Space+Drag | Pan |
| R | Rectangle selection |
| O | Ellipse selection |
| L | Lasso selection |
| Enter | Add selection to mask |
| Shift+Enter | Subtract selection from mask |
| Escape | Clear selection |
| Ctrl+Shift+I | Invert selection |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+D | Duplicate selection |
| [ / ] | Decrease/increase brush size |
| Ctrl+0 | Fit to view |
| Ctrl+1 | Actual size (100%) |
| Delete | Delete selected object |

---

## Phase 6: Outpainting Support ⏸️ PENDING RESEARCH

### Objectives
Enable canvas extension beyond original image boundaries for outpainting workflows.

### Blockers
> **Pending**: ComfyUI outpainting workflow research needed before implementation.

### Open Questions
1. **Mask handling**: Should outpaint mask be separate from inpaint mask, or combined into single output?
2. **Extension mode**: Prefer directional (add 256px to right) or target resolution (extend to 1920x1080)?
3. **Auto-mask behavior**: Should extended areas automatically become masked, or require user confirmation?
4. **Workflow integration**: How does the outpainting workflow differ from inpainting in ComfyUI nodes?
5. **Model requirements**: Are there specific model requirements for outpainting (e.g., inpaint models)?

### Planned Deliverables (Tentative)

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

## Known Issues & Future Improvements

### Known Issues
> Document any bugs or issues discovered during development here.

*None currently documented.*

### Future Improvements
> Ideas and enhancements to consider for future phases.

1. **Performance**: Consider WebGL rendering for very large images
2. **Undo/Redo**: Currently uses JSON serialization; could optimize with delta states
3. **Layer Panel**: Could add thumbnail previews for each layer
4. **Selection**: Could add "Select All" and "Select Inverse" for mask region
5. **Mask**: Could add feathering/blur to mask edges for softer inpainting

---

## Design Decisions Log

### Mask System
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Mask overlay opacity stacking | Fixed with offscreen compositing | Better UX, matches user expectations |
| Mask visual feedback | Static stripe pattern (baked into image) | Clear indication of mask mode, minimal performance impact |
| Animated pattern | Rejected | CSS animation caused performance issues |
| Glow effect | Rejected | Nice visually but GPU resource cost not justified |
| Mask color | Customizable via context-aware color picker | Reuses existing UI, brush color synced with overlay |
| Selection to mask mode | Additive | User can clear mask if they want to replace |
| Mask export format | Binary B/W PNG | Required for AI inpainting workflows |
| Marching ants preview | Static dashed lines with caching | Animation caused significant performance hit |

### Selection System
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Selection visualization | Marching ants (static dashed) | Classic selection appearance, performant |
| Invert selection | Direct to mask (not visual inversion) | Complex path boolean avoided, simpler workflow |
| Tool cycling | Click cycles when already active | Matches mask preview mode cycling UX |
| Selection object storage | Single active selection | Simpler than multi-selection for mask workflow |

### Layer System
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Raster vs Vector | Vector (Fabric.js paths) | Enables undo/redo, scales well |
| Pixel-level erasing | Deferred to Phase 7 | Requires rasterization, complex |
| Layer blending | Deferred to Phase 8 | Normal blending sufficient for now |

### Outpainting
| Decision | Choice | Rationale |
|----------|--------|-----------|
| Implementation timing | Phase 6 (paused) | Depends on ComfyUI workflow research |
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
| `wwwroot/js/ImageEditor/ImageEditor.js` | Main entry point, mixin composition |
| `wwwroot/js/ImageEditor/ImageEditor.layers.js` | Layer management |
| `wwwroot/js/ImageEditor/ImageEditor.canvas.js` | Canvas operations, zoom/pan |
| `wwwroot/js/ImageEditor/ImageEditor.tools.js` | Tool handling, brush setup |
| `wwwroot/js/ImageEditor/ImageEditor.history.js` | Undo/redo system |
| `wwwroot/js/ImageEditor/ImageEditor.export.js` | Image/mask export |
| `wwwroot/js/ImageEditor/ImageEditor.events.js` | Event handlers (mouse, keyboard) |
| `wwwroot/js/ImageEditor/ImageEditor.callbacks.js` | .NET interop callbacks |
| `wwwroot/js/ImageEditor/ImageEditor.utils.js` | Utility functions |
| `wwwroot/js/ImageEditor/ImageEditor.mask.js` | Mask compositing, preview modes (Phase 4) |
| `wwwroot/js/ImageEditor/ImageEditor.selection.js` | Selection tools (Phase 5) |
| `Models/ImageEditorState.cs` | C# state model |
| `Services/ManagerService.cs` | State storage |

---

## Changelog

### Version 6.0 (December 2024) - Phase 5 Complete, Development Paused

#### Phase 5: Selection Tools ✅ COMPLETE
- Created `ImageEditor.selection.js` - new mixin for selection system
- Implemented rectangle, ellipse, and lasso selection tools
- Selection modifiers: Shift (constrain), Alt (center-out)
- Selection to mask: Add and subtract operations
- Invert selection: Applies inverted area directly to mask
- Marching ants visualization (static dashed for performance)
- Toolbar integration with cycling behavior
- Contextual action buttons when selection exists
- Keyboard shortcuts: R, O, L, Enter, Shift+Enter, Escape, Ctrl+Shift+I

#### Mask Preview Modes Enhanced
- Added 5 preview modes: Overlay, Binary, Marching Ants, Blackout, Whiteout
- Toolbar dropdown with click-to-cycle behavior
- Marching ants uses cached canvas for performance

#### Document Updates
- Added Current Architecture section with file structure and patterns
- Added keyboard shortcuts reference
- Added Known Issues & Future Improvements section
- Updated status to reflect development pause

### Version 5.0 (December 2024) - Phase 4 Complete

#### Phase 4: Mask Optimization ✅ COMPLETE
- Created `ImageEditor.mask.js` - new mixin for mask system
- Implemented offscreen canvas compositing for flat mask rendering
- Individual mask strokes hidden, overlay shows unified mask
- Binary B/W mask export via `exportMaskBinary()`
- Context-aware color picker (brush vs mask based on active tool)
- Mask brush color synced with display overlay color
- Static diagonal stripe pattern for visual distinction (performance-friendly)
- Mask overlay syncs with viewport transformations (zoom/pan)
- State restoration properly refreshes mask overlay
- Visibility toggle correctly refreshes overlay

### Version 4.0 (December 2024) - Phase Planning Update

#### Document Restructure
- Added Phases 4-8 with detailed planning
- Moved completed phases to summary section
- Added Design Decisions Log
- Added Open Questions for outpainting

### Version 3.7 (December 2024) - Phase 3 Complete
- Object management: Copy/Paste/Duplicate/Flip
- Fabric.js 6.x async clone compatibility
- Toolbar buttons for object actions

### Previous Versions
See git history for earlier changelog entries.

---

## Implementation Checklist

### Phase 4: Mask Optimization ✅ COMPLETE
- [x] Create offscreen canvas for mask compositing
- [x] Render mask strokes as union (no opacity stacking)
- [x] Update mask display after each stroke
- [x] Implement static stripe pattern overlay
- [x] Add mask color to color picker (context-aware)
- [x] Sync mask brush color with overlay color
- [x] Ensure binary B/W export
- [x] Refresh overlay on visibility toggle
- [x] Refresh overlay on state restoration
- [x] Sync overlay with viewport (zoom/pan)
- [x] Add multiple preview modes (Overlay, Binary, Marching Ants, Blackout, Whiteout)

### Phase 5: Selection Tools ✅ COMPLETE
#### Infrastructure
- [x] Create `ImageEditor.selection.js` with SelectionMixin
- [x] Add selection tool types to `ImageEditorTool` enum (SelectRect, SelectEllipse, SelectLasso)
- [x] Add selection state to `ImageEditorState.cs` (HasSelection, ActiveSelectionType)
- [x] Wire up SelectionMixin in main ImageEditor.js

#### UI Components
- [x] Add Selection Tools button group with dropdown menu
- [x] Implement dropdown for tool type switching (rect/ellipse/lasso)
- [x] Add contextual action buttons (Add to Mask, Subtract, Clear, Invert)
- [x] Show/hide action buttons based on selection state
- [x] Implement tool cycling (click cycles when already active)

#### Selection Tools
- [x] Implement rectangle selection (click-drag)
- [x] Implement ellipse selection (click-drag)
- [x] Implement lasso/freeform selection (click-drag path)
- [x] Add Shift modifier for square/circle constraint
- [x] Add Alt modifier for center-out drawing
- [x] Add Shift+Alt combined constraint

#### Selection Visualization
- [x] Create selection object with dashed stroke
- [x] Add semi-transparent fill
- [x] Implement marching ants (static dashed for performance)

#### Selection to Mask
- [x] Implement "Add to Mask" (fill selection as mask, additive)
- [x] Implement "Subtract from Mask" (remove selection area from mask)
- [x] Auto-clear selection after mask operation
- [x] Update mask overlay after operation

#### Selection Management
- [x] Implement "Clear Selection" with toolbar button
- [x] Implement Escape key to clear selection
- [x] Implement "Invert Selection" with toolbar button
- [x] Implement Ctrl+Shift+I shortcut for invert

#### Keyboard Shortcuts
- [x] R key for Rectangle Select
- [x] O key for Ellipse Select
- [x] L key for Lasso Select
- [x] Enter for Add to Mask
- [x] Shift+Enter for Subtract from Mask
- [x] Escape for Clear Selection
- [x] Ctrl+Shift+I for Invert Selection

### Phase 6: Outpainting ⏸️ PENDING RESEARCH
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

---

## Session Resume Context

> **For AI assistants resuming work on this project:**

### Quick Context
1. **What is this?** A Fabric.js-based image editor in a Blazor WebApp for AI image generation workflows
2. **Current state**: Phases 1-5 complete (core editor, drawing, layers, masks, selections)
3. **Next step**: Phase 6 (outpainting) blocked on ComfyUI workflow research
4. **Tech stack**: Blazor Server (.NET 8), Fabric.js 6.x, JavaScript mixins

### Key Files to Review
1. `ImageEditorModal.razor` - Main component, all toolbar UI
2. `ImageEditor.js` - Entry point, see how mixins are composed
3. `ImageEditor.mask.js` - Mask system architecture
4. `ImageEditor.selection.js` - Selection tools architecture
5. `ImageEditorState.cs` - C# state model

### Architecture Notes
- JS uses mixin pattern (`Object.assign(prototype, Mixin)`)
- Mask strokes are hidden Fabric.js objects; display uses HTML overlay
- Selection uses Fabric.js shapes with special `isSelection` flag
- Blazor ↔ JS communication via `DotNetObjectReference` and `IJSObjectReference`

### Common Patterns
```javascript
// Notify Blazor of state change
this._notifySelectionChanged(hasSelection);
this._notifyMaskChanged(hasMask);

// Access base image dimensions
this.baseImageObject.width / height

// Canvas viewport transform
const vpt = this.canvas.viewportTransform;
const zoom = vpt[0], panX = vpt[4], panY = vpt[5];
```

### Testing Notes
- Test with various image sizes (small, large, very large)
- Test zoom/pan with mask and selection overlays
- Test Save vs Apply workflow (Save preserves layers, Apply flattens)
