# Image Editor Implementation Plan

> **Document Version:** 5.1  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 4 Complete, Phase 5 Planned  
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
| **Phase 4** | Mask Optimization | ✅ Complete | P0 |
| **Phase 5** | Selection Tools | 📋 Planned | P1 |
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

### Phase 4: Mask Optimization ✅
- **Flat mask rendering**: Offscreen canvas compositing eliminates opacity stacking
- **Binary mask export**: Pure black/white PNG output for AI workflows
- **Visual overlay**: Colored mask with subtle diagonal stripe pattern
- **Context-aware color picker**: Brush color vs mask color based on active tool
- **Mask brush color sync**: Brush stroke color matches display overlay color
- **State restoration**: Mask overlay properly refreshes on visibility toggle and session restore
- **Viewport sync**: Mask overlay follows zoom/pan transformations

**New File Created**: `wwwroot/js/ImageEditor/ImageEditor.mask.js`

---

## Phase 5: Selection Tools 📋 PLANNED

### Objectives
Enable precise area selection for mask creation with Photoshop-like workflow.

### Implementation Approach
- Use Fabric.js shapes (Rect, Ellipse, Path) as selection objects
- Distinct visual style: dashed stroke with semi-transparent fill
- Marching ants animation for selection border (evaluate performance)

### Planned Deliverables

#### 5.1 Selection Tool Types
| Item | Description | Icon | Shortcut | Status |
|------|-------------|------|----------|--------|
| Rectangle Select | Click-drag rectangle | `CropSquare` | R | 🔴 |
| Ellipse Select | Click-drag ellipse | `RadioButtonUnchecked` | O | 🔴 |
| Lasso Select | Freeform polygon/path | `Gesture` | L | 🔴 |

**UI Pattern**: Dropdown menu within selection button group
- Click active tool icon to use current selection type
- Click dropdown arrow to switch selection type
- Visual indicator shows current selection type

#### 5.2 Selection Visualization
| Item | Description | Status |
|------|-------------|--------|
| Dashed stroke | Animated marching ants border | 🔴 |
| Semi-transparent fill | Light fill to show selected area | 🔴 |
| Performance evaluation | May fall back to static dashed if animation impacts perf | 🔴 |

#### 5.3 Selection Modifiers (Photoshop-style)
| Modifier | Behavior | Status |
|----------|----------|--------|
| Shift | Constrain to square (rect) or circle (ellipse) | 🔴 |
| Alt | Draw from center outward | 🔴 |
| Shift+Alt | Both constraints combined | 🔴 |

#### 5.4 Selection to Mask Actions
| Item | Description | Location | Shortcut | Status |
|------|-------------|----------|----------|--------|
| Add to Mask | Fill selection area into mask (additive) | Toolbar (contextual) | Enter | 🔴 |
| Subtract from Mask | Remove selection area from mask | Toolbar (contextual) | Shift+Enter | 🔴 |
| Clear after apply | Selection auto-clears after mask operation | Automatic | - | 🔴 |

**Toolbar Pattern**: Selection action buttons appear contextually when:
- A selection tool is active, AND
- An active selection exists on canvas

#### 5.5 Selection Management
| Item | Description | Location | Shortcut | Status |
|------|-------------|----------|----------|--------|
| Clear Selection | Deselect current selection | Toolbar button | Escape | 🔴 |
| Invert Selection | Flip selected/unselected areas | Toolbar button | Ctrl+Shift+I | 🔴 |

### Technical Implementation Notes

#### New Tool Types (ImageEditorTool enum)
```csharp
SelectRect,      // Rectangle selection
SelectEllipse,   // Ellipse selection  
SelectLasso      // Freeform lasso selection
```

#### Selection State
```csharp
// In ImageEditorState.cs
public bool HasSelection { get; set; }
public string? ActiveSelectionType { get; set; } // "rect", "ellipse", "lasso"
```

#### JavaScript Module
Create `ImageEditor.selection.js` with SelectionMixin:
- `_initSelectionSystem()` - Initialize selection state
- `startRectSelection(x, y)` - Begin rectangle selection
- `startEllipseSelection(x, y)` - Begin ellipse selection
- `startLassoSelection(x, y)` - Begin lasso selection
- `updateSelection(x, y)` - Update selection during drag
- `finishSelection()` - Complete selection
- `clearSelection()` - Remove current selection
- `invertSelection()` - Invert selection area
- `selectionToMask(mode)` - Convert selection to mask ('add' or 'subtract')
- `_renderMarchingAnts()` - Animate selection border

#### Marching Ants Implementation
```javascript
// CSS-based animation for performance
.selection-object {
    stroke-dasharray: 5, 5;
    animation: marching-ants 0.5s linear infinite;
}

@keyframes marching-ants {
    to { stroke-dashoffset: -10; }
}
```

### Toolbar Layout (Updated)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [Drawing Tools] | [Mask Tools] | [Selection Tools▼] | [Color] [Size] | ... │
│                                  ├─────────────────┤                        │
│                                  │ ⬜ Rectangle    │                        │
│                                  │ ⭕ Ellipse      │                        │
│                                  │ ✏️ Lasso        │                        │
│                                  └─────────────────┘                        │
└─────────────────────────────────────────────────────────────────────────────┘

When selection exists:
┌─────────────────────────────────────────────────────────────────────────────┐
│ ... [Selection Tools▼] | [➕ Add to Mask] [➖ Subtract] [🚫 Clear] [⟲ Invert] │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Workflow Example

1. User clicks Selection dropdown, chooses "Rectangle"
2. User draws rectangle on canvas (with Shift for square constraint)
3. Marching ants appear around selection
4. "Add to Mask" and "Subtract from Mask" buttons appear in toolbar
5. User clicks "Add to Mask"
6. Selection area is filled into mask layer
7. Selection auto-clears
8. Mask overlay updates to show new masked area

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
| Mask overlay opacity stacking | Fixed with offscreen compositing | Better UX, matches user expectations |
| Mask visual feedback | Static stripe pattern (baked into image) | Clear indication of mask mode, minimal performance impact |
| Animated pattern | Rejected | CSS animation caused performance issues |
| Glow effect | Rejected | Nice visually but GPU resource cost not justified |
| Mask color | Customizable via context-aware color picker | Reuses existing UI, brush color synced with overlay |
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
| `wwwroot/js/ImageEditor/ImageEditor.mask.js` | Mask compositing & overlay (Phase 4) |
| `Models/ImageEditorState.cs` | C# state model |
| `Services/ManagerService.cs` | State storage |

---

## Changelog

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

#### Design Decisions Updated
- Rejected CSS animation for performance reasons
- Rejected glow effect to save GPU resources for generation
- Chose static baked-in stripe pattern as final solution

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

### Phase 5: Selection Tools
#### Infrastructure
- [ ] Create `ImageEditor.selection.js` with SelectionMixin
- [ ] Add selection tool types to `ImageEditorTool` enum (SelectRect, SelectEllipse, SelectLasso)
- [ ] Add selection state to `ImageEditorState.cs` (HasSelection, ActiveSelectionType)
- [ ] Wire up SelectionMixin in main ImageEditor.js

#### UI Components
- [ ] Add Selection Tools button group with dropdown menu
- [ ] Implement dropdown for tool type switching (rect/ellipse/lasso)
- [ ] Add contextual action buttons (Add to Mask, Subtract, Clear, Invert)
- [ ] Show/hide action buttons based on selection state

#### Selection Tools
- [ ] Implement rectangle selection (click-drag)
- [ ] Implement ellipse selection (click-drag)
- [ ] Implement lasso/freeform selection (click-drag path)
- [ ] Add Shift modifier for square/circle constraint
- [ ] Add Alt modifier for center-out drawing
- [ ] Add Shift+Alt combined constraint

#### Selection Visualization
- [ ] Create selection object with dashed stroke
- [ ] Add semi-transparent fill
- [ ] Implement marching ants CSS animation
- [ ] Evaluate animation performance, fallback to static if needed

#### Selection to Mask
- [ ] Implement "Add to Mask" (fill selection as mask, additive)
- [ ] Implement "Subtract from Mask" (remove selection area from mask)
- [ ] Auto-clear selection after mask operation
- [ ] Update mask overlay after operation

#### Selection Management
- [ ] Implement "Clear Selection" with toolbar button
- [ ] Implement Escape key to clear selection
- [ ] Implement "Invert Selection" with toolbar button
- [ ] Implement Ctrl+Shift+I shortcut for invert

#### Keyboard Shortcuts
- [ ] R key for Rectangle Select
- [ ] O key for Ellipse Select
- [ ] L key for Lasso Select
- [ ] Enter for Add to Mask
- [ ] Shift+Enter for Subtract from Mask
- [ ] Escape for Clear Selection
- [ ] Ctrl+Shift+I for Invert Selection

#### Testing
- [ ] Test selection creation with all three types
- [ ] Test modifier keys (Shift, Alt)
- [ ] Test selection to mask workflow (add and subtract)
- [ ] Test selection persistence during zoom/pan
- [ ] Test undo/redo with selections
- [ ] Evaluate marching ants performance

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
