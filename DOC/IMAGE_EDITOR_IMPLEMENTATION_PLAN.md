# Image Editor Implementation Plan

> **Document Version:** 3.0  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 3 Planning Complete  
> **Target:** Img2ImgComfyUI Page Enhancement

---

## Executive Summary

The Image Editor is a Fabric.js-based drawing and masking tool integrated into the Blazor WebApp. It enables users to edit images before sending them to ComfyUI for img2img generation, and to create inpainting masks for targeted image modifications.

**Phase 3** introduces a layer system for compositing, allowing users to import external images, organize content into layers, and build complex compositions for AI image generation.

---

## Phase Overview

| Phase | Description | Status | Priority |
|-------|-------------|--------|----------|
| **Phase 1** | Core Canvas Infrastructure | ✅ Complete | P0 |
| **Phase 2** | Drawing & Mask Tools | ✅ Complete | P0 |
| **Phase 3** | Layer System & Compositing | 🟡 Planning Complete | P1 |
| **Phase 4** | Polish & UX | 🔴 Not Started | P2 |

---

## Phase 1: Core Canvas Infrastructure ✅ COMPLETE

### Deliverables
- [x] Fabric.js integration with Blazor
- [x] Modal component structure
- [x] Zoom/pan functionality
- [x] JS interop communication
- [x] Canvas initialization and disposal

---

## Phase 2: Drawing & Mask Tools ✅ COMPLETE

### Deliverables
- [x] Brush tool with configurable size/color
- [x] Eraser tool (removes intersecting strokes)
- [x] Mask brush and mask eraser
- [x] Color picker (eyedropper)
- [x] Undo/Redo system
- [x] Keyboard shortcuts
- [x] Export image and mask as PNG

---

## Phase 3: Layer System & Compositing 🟡 PLANNING COMPLETE

### Objectives
- Implement logical layer system using Fabric.js object properties
- Enable image import for photo compositing
- Provide layer management UI (visibility, opacity, ordering, renaming)
- Support multiple drawing layers
- Preserve editor state across sessions
- Add "Flatten & Apply" for committing compositions

### Architecture Decision: Option A - Object Property Grouping

Each Fabric.js object has a `layer` property that groups it logically:

```javascript
// Example object structure
{
    type: 'image',
    name: 'imported',
    layer: 'Background Elements',  // Logical layer grouping
    opacity: 1.0,
    // ... other Fabric properties
}
```

**Benefits:**
- Minimal code changes to existing infrastructure
- Leverages Fabric's built-in z-ordering
- Objects remain individually selectable/movable
- Layer operations (hide, delete, opacity) apply to all objects with matching `layer` property

**Trade-offs (acceptable for our use case):**
- Effects/filters apply per-object, not per-layer
- No true layer isolation (Photoshop-style)

---

### Phase 3.1: Core Layer Infrastructure

#### Objectives
- Add layer tracking to state management
- Assign layers to new objects
- Track active/selected layer

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| Layer property | Add `layer` property to all Fabric objects | 🟢 Low |
| Active layer tracking | Track currently selected layer in JS state | 🟢 Low |
| Layer assignment | New drawings assigned to active layer | 🟢 Low |
| Layer data model | Add layer list to `ImageEditorState.cs` | 🟢 Low |
| Default layers | Create default "Base" and "Drawing" layers | 🟢 Low |

#### Data Model

```csharp
// ImageEditorState.cs additions
public class LayerInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Layer";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public float Opacity { get; set; } = 1.0f;
    public int Order { get; set; } // Z-index order
}

public List<LayerInfo> Layers { get; set; } = new();
public string ActiveLayerId { get; set; }
```

---

### Phase 3.2: Image Import

#### Objectives
- Allow users to import PNG/JPG/WebP images
- Support drag-drop, file picker, and clipboard paste
- Imported images are movable, resizable, rotatable

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| Drag-drop import | Drop image file onto canvas | 🟢 Low |
| File picker button | Toolbar button opens file dialog | 🟢 Low |
| Clipboard paste | Ctrl+V pastes image from clipboard | 🟡 Medium |
| Transform controls | Fabric built-in: move, resize, rotate | 🟢 Low (built-in) |
| Layer assignment | Imported images assigned to active layer | 🟢 Low |
| File type support | PNG, JPG, WebP | 🟢 Low |

#### Implementation Notes

```javascript
// Drag-drop handling
canvas.on('drop', async (e) => {
    const file = e.e.dataTransfer.files[0];
    if (file && file.type.startsWith('image/')) {
        const dataUrl = await readFileAsDataURL(file);
        addImportedImage(dataUrl);
    }
});

// Add imported image
function addImportedImage(dataUrl) {
    fabric.Image.fromURL(dataUrl, (img) => {
        img.set({
            left: canvas.width / 2,
            top: canvas.height / 2,
            originX: 'center',
            originY: 'center',
            selectable: true,
            hasControls: true,
            hasBorders: true,
            name: 'imported',
            layer: this.activeLayer
        });
        // Scale down if larger than canvas
        const maxSize = Math.min(canvas.width, canvas.height) * 0.8;
        if (img.width > maxSize || img.height > maxSize) {
            const scale = maxSize / Math.max(img.width, img.height);
            img.scale(scale);
        }
        canvas.add(img);
        canvas.setActiveObject(img);
    });
}
```

---

### Phase 3.3: Layer Panel UI

#### Objectives
- Collapsible right sidebar for layer management
- Visual layer list with controls
- Layer selection, reordering, and editing

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| Panel component | Collapsible right sidebar | 🟡 Medium |
| Layer list | Display layers with icons | 🟢 Low |
| Visibility toggle | Eye icon to show/hide layer | 🟢 Low |
| Lock toggle | Lock icon to prevent edits | 🟢 Low |
| Layer rename | Double-click to edit name | 🟢 Low |
| Layer reorder | Drag or up/down buttons | 🟡 Medium |
| Active layer indicator | Highlight selected layer | 🟢 Low |
| Add layer button | Create new empty layer | 🟢 Low |
| Delete layer button | Remove layer and its objects | 🟢 Low |
| Opacity slider | Per-layer opacity control | 🟡 Medium |

#### UI Mockup

```
┌─────────────────────────────┐
│ Layers                   [×]│  ← Collapse button
├─────────────────────────────┤
│ 👁 🔒 Mask Layer         ▲  │  ← Always on top
├─────────────────────────────┤
│ 👁 🔒 Character              │  ← Active layer (highlighted)
│ 👁 🔒 Background Elements    │
│ 👁 🔒 Drawing Layer          │
│ 👁 🔒 Base Image          ▼  │  ← Always at bottom
├─────────────────────────────┤
│ Opacity: [========●==] 100% │  ← Selected layer opacity
├─────────────────────────────┤
│ [+ Add]  [🗑 Delete]  [⬆][⬇]│
└─────────────────────────────┘
```

#### Special Layers

| Layer | Behavior |
|-------|----------|
| **Base Image** | Always at bottom, not deletable, locked by default |
| **Mask Layer** | Always on top, not deletable, only mask objects |

---

### Phase 3.4: Object Management

#### Objectives
- Keyboard shortcuts for common operations
- Object manipulation tools

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| Delete selected | Delete/Backspace removes selection | 🟢 Low |
| Copy | Ctrl+C copies selected object(s) | 🟢 Low |
| Paste | Ctrl+V pastes copied object(s) | 🟢 Low |
| Duplicate | Ctrl+D duplicates in place | 🟢 Low |
| Flip horizontal | Mirror object horizontally | 🟢 Low |
| Flip vertical | Mirror object vertically | 🟢 Low |
| Select all in layer | Click layer to select its objects | 🟡 Medium |

#### Keyboard Shortcuts (New)

| Shortcut | Action |
|----------|--------|
| Delete / Backspace | Delete selected object(s) |
| Ctrl+C | Copy selected |
| Ctrl+V | Paste (image from clipboard OR copied objects) |
| Ctrl+D | Duplicate selected |
| Ctrl+A | Select all objects in active layer |

---

### Phase 3.5: State Preservation

#### Objectives
- Preserve layer state across editor sessions
- Support "Apply" (preserve) and "Flatten & Apply" (reset) modes

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| State serialization | Save full layer state to `ImageEditorState` | 🟡 Medium |
| State restoration | Restore layers when reopening editor | 🟡 Medium |
| Apply button | Flatten visible → Update input → Preserve state | 🟡 Medium |
| Flatten & Apply | Flatten → Update input → Reset state (new base) | 🟡 Medium |
| UI for flatten | Menu or button for "Flatten & Apply" | 🟢 Low |

#### Workflow: Apply vs Flatten & Apply

**Apply (Preserve State)**
```
User edits → Clicks Apply → 
  1. Flatten visible layers to PNG
  2. Send PNG to Blazor (input image updated)
  3. Layer state preserved in ImageEditorState
  4. User can reopen and continue editing layers
```

**Flatten & Apply (Reset State)**
```
User edits → Clicks Flatten & Apply →
  1. Flatten visible layers to PNG
  2. Send PNG to Blazor (input image updated)
  3. Layer state RESET
  4. Flattened image becomes new "Base Image" layer
  5. User reopens with clean slate (single base layer)
```

**Use Case:**
- Build complex composition with 10+ imported elements
- "Flatten & Apply" to commit as single base image
- Import more elements on top
- Prevents unbounded layer growth

---

### Phase 3.6: Integration & Limits

#### Deliverables

| Item | Description | Complexity |
|------|-------------|------------|
| Layer limit | Maximum 20 layers (configurable) | 🟢 Low |
| Object limit per layer | Soft limit warning at 50 objects | 🟢 Low |
| Import size limit | Scale down images > 4096px | 🟢 Low |
| Layer in undo/redo | Layer changes included in history | 🟡 Medium |

---

### Deferred to Future Phase (Raster Layer Phase)

| Feature | Reason |
|---------|--------|
| Raster drawing layers | Requires hybrid canvas architecture |
| Pixel-level eraser | Requires raster layers |
| Image effects (brightness, contrast) | Requires per-layer raster processing |
| Blending modes (multiply, overlay, etc.) | Complex, low priority |
| Layer thumbnails | Performance overhead, low value |
| Layer merge/flatten (selective) | Nice to have, not critical |

---

## Phase 4: Polish & UX 🔴 NOT STARTED

### Planned Deliverables
- Performance optimization (large images, many layers)
- History state compression
- Save/load editor presets
- Recent colors palette
- Touch device support
- Accessibility improvements

---

## Technical Specifications

### Layer Data Flow

```
Blazor Component                    ImageEditor.js
      │                                   │
      │──── Open editor ─────────────────►│
      │     (with LayerState JSON)        │
      │                                   │
      │◄─── Restore layers ───────────────│
      │                                   │
      │     [User edits layers]           │
      │                                   │
      │◄─── OnLayerChanged() ─────────────│
      │     (layer added/removed/modified)│
      │                                   │
      │──── Apply ────────────────────────►│
      │                                   │
      │◄─── Return (image, mask, state) ──│
      │                                   │
```

### Object Layer Property

All Fabric objects include:

```javascript
{
    name: 'drawing' | 'imported' | 'mask' | 'baseImage',
    layer: 'Layer Name',        // Logical layer grouping
    layerId: 'guid-string',     // For precise matching
    // ... standard Fabric properties
}
```

### Layer State Serialization

```javascript
// On Apply, serialize:
{
    layers: [
        { id: 'abc', name: 'Background', visible: true, locked: false, opacity: 1.0, order: 0 },
        { id: 'def', name: 'Characters', visible: true, locked: false, opacity: 0.8, order: 1 },
        // ...
    ],
    activeLayerId: 'def',
    objects: [...], // Full Fabric canvas JSON
}
```

---

## File Reference

### Core Files
| File | Purpose |
|------|---------|
| `Components/ImageEditor/ImageEditorModal.razor` | Main Blazor component |
| `Components/ImageEditor/ImageEditorModal.razor.css` | Scoped CSS styles |
| `Components/ImageEditor/LayerPanel.razor` | **NEW: Layer panel component** |
| `wwwroot/js/ImageEditor.js` | Fabric.js integration |
| `Models/ImageEditorState.cs` | C# state model (with layer info) |

### External Dependencies
| Dependency | Version | CDN |
|------------|---------|-----|
| Fabric.js | 6.0.2 | jsdelivr, unpkg, cdnjs |

---

## Changelog

### Version 3.0 (December 2024) - Phase 3 Planning
- **Comprehensive Phase 3 specification**
  - Layer system using object property grouping (Option A)
  - Image import via drag-drop, file picker, clipboard
  - Layer panel UI with visibility, lock, opacity, rename, reorder
  - Multiple drawing layers (draw to active layer)
  - State preservation across editor sessions
  - "Apply" (preserve state) vs "Flatten & Apply" (reset state)
- **Technical decisions documented**
  - 20 layer limit
  - Object property grouping vs multiple canvases
  - Hybrid approach for imported images (grouped by layer)
- **Deferred features identified** for future Raster Layer Phase

### Version 2.3 (December 2024) - Phase 2 Complete
- Eraser tool removes intersecting strokes
- Phase 2 marked complete

### Version 2.2 (December 2024) - Undo/Redo Fix
- Complete undo/redo functionality
- Fabric.js 6 custom property serialization

---

## Implementation Checklist

### Phase 3.1: Core Layer Infrastructure
- [ ] Add `layer` and `layerId` properties to object creation
- [ ] Add `LayerInfo` class to `ImageEditorState.cs`
- [ ] Track `activeLayerId` in JS state
- [ ] Assign active layer to new drawings
- [ ] Create default layers (Base Image, Drawing Layer)

### Phase 3.2: Image Import
- [ ] Implement drag-drop handler on canvas
- [ ] Add import button to toolbar
- [ ] Implement clipboard paste (Ctrl+V)
- [ ] Scale imported images to fit canvas
- [ ] Assign imports to active layer

### Phase 3.3: Layer Panel UI
- [ ] Create `LayerPanel.razor` component
- [ ] Implement collapsible sidebar
- [ ] Layer list with visibility/lock toggles
- [ ] Double-click to rename layer
- [ ] Add/delete layer buttons
- [ ] Layer reordering (drag or buttons)
- [ ] Opacity slider for selected layer

### Phase 3.4: Object Management
- [ ] Delete key handler
- [ ] Copy/paste (Ctrl+C/V)
- [ ] Duplicate (Ctrl+D)
- [ ] Flip horizontal/vertical
- [ ] Select all in layer

### Phase 3.5: State Preservation
- [ ] Serialize layer state on Apply
- [ ] Restore layer state on editor open
- [ ] "Flatten & Apply" button/menu
- [ ] Reset state after flatten

### Phase 3.6: Integration
- [ ] Layer limit enforcement (20)
- [ ] Import size limit (4096px)
- [ ] Layer operations in undo/redo

---

*Document maintained in: `DOC/IMAGE_EDITOR_IMPLEMENTATION_PLAN.md`*
