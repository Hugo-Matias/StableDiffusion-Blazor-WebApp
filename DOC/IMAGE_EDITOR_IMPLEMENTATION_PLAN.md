# Image Editor Implementation Plan

> **Document Version:** 3.1  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 3.1 Complete  
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
| **Phase 3** | Layer System & Compositing | 🟡 In Progress | P1 |
| **Phase 4** | Polish & UX | 🔴 Not Started | P2 |

### Phase 3 Sub-phases

| Sub-phase | Description | Status |
|-----------|-------------|--------|
| **3.1** | Core Layer Infrastructure | ✅ Complete |
| **3.2** | Image Import | 🔴 Not Started |
| **3.3** | Layer Panel UI | 🔴 Not Started |
| **3.4** | Object Management | 🟡 Partial |
| **3.5** | State Preservation | ✅ Complete |
| **3.6** | Integration & Limits | 🟡 Partial |

---

## Phase 1 & 2: COMPLETE

See previous documentation for Phase 1 and 2 details.

---

## Phase 3: Layer System & Compositing

### Phase 3.1: Core Layer Infrastructure ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| `LayerInfo` class | Layer data model with Id, Name, Visibility, Lock, Opacity, Order, Type | ✅ |
| `LayerType` enum | Base, Drawing, Import, Mask layer types | ✅ |
| Layer property on objects | All Fabric objects have `layer`, `layerId`, `name` properties | ✅ |
| Active layer tracking | `activeLayerId` in JS and C# state | ✅ |
| Layer assignment | New drawings assigned to active layer | ✅ |
| Default layers | "Base Image" (locked) and "Drawing Layer" created on init | ✅ |
| `InitializeDefaultLayers()` | Reset creates proper default layers | ✅ |
| Layer state in undo/redo | Layer properties preserved in serialization | ✅ |
| Select tool (V) | Tool for selecting/moving objects on canvas | ✅ |
| Layer-aware erasing | Eraser only affects objects on active layer | ✅ |

#### Data Model (ImageEditorState.cs)

```csharp
public class LayerInfo
{
    public const string BaseLayerId = "base-image-layer";
    public const string MaskLayerId = "mask-layer";
    
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public float Opacity { get; set; }
    public int Order { get; set; }
    public LayerType LayerType { get; set; }
    
    public bool CanDelete => LayerType != LayerType.Base && LayerType != LayerType.Mask;
    public bool CanReorder => LayerType != LayerType.Base && LayerType != LayerType.Mask;
}

public enum LayerType { Base, Drawing, Import, Mask }
```

#### JavaScript Layer Methods (ImageEditor.js)

```javascript
// Layer management
_initializeDefaultLayers()
getActiveLayer()
setActiveLayer(layerId)
addLayer(name, type)
removeLayer(layerId)
updateLayer(layerId, props)
getLayers()
getLayerState()
restoreLayerState(state)

// Layer-aware operations
_updateLayerVisibility(layerId, visible)
_updateLayerOpacity(layerId, opacity)
_reorderCanvasObjects()
```

---

### Phase 3.5: State Preservation ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| Save button | Exports flattened image, preserves original base + layers | ✅ |
| Apply button | Flattens to new base, resets layer state | ✅ |
| Separate base preservation | `BaseImageData` stored separately from output | ✅ |
| Canvas JSON storage | Full canvas state saved in `CanvasJson` for re-editing | ✅ |
| State restoration | Layers and strokes restored when reopening editor | ✅ |
| Tool state sync | Current tool synced with JS on editor reopen | ✅ |
| Layer count display | Shows "Layers: X / 20" in footer | ✅ |
| Active layer indicator | Chip in title bar shows current layer | ✅ |

#### Save vs Apply Workflow

| Action | Output Image | Base Image (State) | Canvas JSON | Layers |
|--------|-------------|-------------------|-------------|--------|
| **Save** | Flattened export | Preserved (original) | Saved | Preserved |
| **Apply** | Flattened export | Updated (flattened) | Cleared | Reset |

**Save**: User can reopen and continue editing with original base + all strokes editable.

**Apply**: Commits all edits permanently. Flattened image becomes new base for fresh start.

---

### Phase 3.2: Image Import 🔴 NOT STARTED

#### Planned Features

| Item | Description | Complexity |
|------|-------------|------------|
| Drag-drop import | Drop image file onto canvas | 🟢 Low |
| File picker button | Toolbar button opens file dialog | 🟢 Low |
| Clipboard paste | Ctrl+V pastes image from clipboard | 🟡 Medium |
| Transform controls | Fabric built-in: move, resize, rotate | 🟢 Low |
| Layer assignment | Imported images assigned to active layer | 🟢 Low |

---

### Phase 3.3: Layer Panel UI 🔴 NOT STARTED

#### Planned Features

| Item | Description | Complexity |
|------|-------------|------------|
| Panel component | Collapsible right sidebar | 🟡 Medium |
| Layer list | Display layers with icons | 🟢 Low |
| Visibility toggle | Eye icon to show/hide layer | 🟢 Low |
| Lock toggle | Lock icon to prevent edits | 🟢 Low |
| Layer rename | Double-click to edit name | 🟢 Low |
| Layer reorder | Drag or up/down buttons | 🟡 Medium |
| Opacity slider | Per-layer opacity control | 🟡 Medium |
| Add/Delete buttons | Create/remove layers | 🟢 Low |

---

### Phase 3.4: Object Management 🟡 PARTIAL

#### Implemented

| Item | Status |
|------|--------|
| Select tool (V) | ✅ |
| Delete selected (Delete/Backspace) | ✅ |
| Object selection in Select mode | ✅ |
| Respect layer lock on delete | ✅ |

#### Remaining

| Item | Status |
|------|--------|
| Copy (Ctrl+C) | 🔴 |
| Paste (Ctrl+V) | 🔴 |
| Duplicate (Ctrl+D) | 🔴 |
| Flip horizontal/vertical | 🔴 |

---

### Phase 3.6: Integration & Limits 🟡 PARTIAL

#### Implemented

| Item | Status |
|------|--------|
| Layer limit constant (20) | ✅ |
| Layer limit in addLayer() | ✅ |

#### Remaining

| Item | Status |
|------|--------|
| Import size limit (4096px) | 🔴 |
| Object limit per layer warning | 🔴 |

---

## Changelog

### Version 3.1 (December 2024) - Phase 3.1 Complete

#### Core Layer Infrastructure
- Added `LayerInfo` class with full property set (Id, Name, IsVisible, IsLocked, Opacity, Order, LayerType)
- Added `LayerType` enum (Base, Drawing, Import, Mask)
- Layer properties on all Fabric objects (`layer`, `layerId`, `name`)
- Active layer tracking in JS (`activeLayerId`) and C# (`ActiveLayerId`)
- Default layers created on initialization ("Base Image" locked, "Drawing Layer" active)
- Layer-aware erasing (only erases from active layer)

#### State Preservation
- **Save button**: Exports flattened image for use, preserves original base image and layer state
- **Apply button**: Flattens all layers to new base, resets state for fresh start
- Separate storage for `BaseImageData` (original) vs output image
- `CanvasJson` stores full canvas state for re-editing
- Proper restoration of layers and strokes on editor reopen

#### Select Tool & Object Management
- Select tool (V) for selecting objects on canvas
- Objects become selectable when Select tool is active (respects layer lock)
- Delete key removes selected objects
- Tool state synced with JS on editor reopen (fixes cursor mismatch bug)

#### UI Updates
- Save/Apply buttons with descriptive tooltips
- Layer count display in footer ("Layers: X / 20")
- Active layer indicator chip in title bar

### Version 3.0 (December 2024) - Phase 3 Planning
- Comprehensive Phase 3 specification
- Technical decisions documented

### Version 2.3 (December 2024) - Phase 2 Complete
- Eraser tool removes intersecting strokes
- Phase 2 marked complete

---

## Implementation Checklist

### Phase 3.1: Core Layer Infrastructure ✅ COMPLETE
- [x] Add `layer` and `layerId` properties to object creation
- [x] Add `LayerInfo` class to `ImageEditorState.cs`
- [x] Add `LayerType` enum
- [x] Track `activeLayerId` in JS state
- [x] Assign active layer to new drawings
- [x] Create default layers (Base Image, Drawing Layer)
- [x] Layer-aware erasing
- [x] Select tool implementation

### Phase 3.5: State Preservation ✅ COMPLETE
- [x] Save button (preserve layers, original base)
- [x] Apply button (flatten, reset state)
- [x] Separate BaseImageData from output
- [x] CanvasJson storage and restoration
- [x] Tool state sync on reopen

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
- [x] Select tool (V)
- [x] Delete key handler
- [ ] Copy/paste (Ctrl+C/V)
- [ ] Duplicate (Ctrl+D)
- [ ] Flip horizontal/vertical

### Phase 3.6: Integration
- [x] Layer limit enforcement (20)
- [ ] Import size limit (4096px)
- [ ] Object limit per layer warning

---

## File Reference

### Core Files
| File | Purpose |
|------|---------|
| `Components/ImageEditor/ImageEditorModal.razor` | Main Blazor component |
| `Components/ImageEditor/ImageEditorModal.razor.css` | Scoped CSS styles |
| `wwwroot/js/ImageEditor.js` | Fabric.js integration with layer support |
| `Models/ImageEditorState.cs` | C# state model with LayerInfo class |

### Key Changes in 3.1
| File | Changes |
|------|---------|
| `ImageEditorState.cs` | Added `LayerInfo`, `LayerType`, layer management methods |
| `ImageEditor.js` | Added layer tracking, `setTool` select mode, layer-aware operations |
| `ImageEditorModal.razor` | Save/Apply buttons, state preservation, tool sync on init |

---

## Testing Notes

### Save/Apply Workflow Verification

1. **Blank Canvas + Save**
   - Create blank canvas, draw strokes
   - Click Save → Strokes visible in output
   - Reopen → Blank canvas with editable strokes on top ✅

2. **Blank Canvas + Apply**
   - Create blank canvas, draw strokes
   - Click Apply → Strokes visible in output
   - Reopen → Flattened image as new base, no editable strokes ✅

3. **Image + Save**
   - Load image, draw strokes
   - Click Save → Original + strokes in output
   - Reopen → Original image with editable strokes on top ✅

4. **Image + Apply**
   - Load image, draw strokes
   - Click Apply → Flattened output
   - Reopen → Flattened image as new base ✅

5. **Tool State Restoration**
   - Select tool, Save, Reopen → Select tool still active ✅

---
