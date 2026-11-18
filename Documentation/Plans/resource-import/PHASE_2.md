# Phase 2 - Import Tab UI

## Status

**Phase:** 2  
**Build Status:** Pending | **Tests:** Manual testing pending

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)

1. **Initial Code Writing** → 2. **Test and Debug Features** → 3. **Discuss Improvements** → 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols

- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)

**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules

- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step
- **All events use pub/sub via EventService.cs**

---

## Objective

Build the Blazor "Import" tab component on the Resources page with file selection, metadata form, cover image picker dialog, and batch import support.

---

## Context

- This phase builds the UI for importing untracked resources
- Uses `ResourcesService.GetUntrackedResources()` from Phase 1
- Uses `ResourcesService.ImportResources()` from Phase 1
- Must follow documented UI design language from `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- Form controls default to `Variant.Text`
- Must use existing layout patterns (`TabbedPageShell`, layout variants)

---

## Execution Checklist

### Step 1: Create ImportResourcePanel.razor with untracked file list

**Complexity:** 5 points
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `ImportResourcePanel.razor` component
- [ ] Display untracked files grouped by detected type
- [ ] Show Active/Inactive badge based on detected path
- [ ] Support selecting single or multiple files for import (checkboxes)
- [ ] Add shared metadata form with fields:
  - Title (text input, defaults to filename)
  - Type (dropdown from ResourceTypes)
  - SubType (autocomplete)
  - Base Model (select from CivitaiService.BaseModelsData.Models)
  - Trigger Words (chip input)
- [ ] Add "Set Cover" button (placeholder for Step 2)
- [ ] Add "Import" button to process selected files
- [ ] Call `ResourcesService.ImportResources()` on import
- [ ] Show success/error feedback

#### Changes Made

{To be updated after implementation}

---

### Step 2: Create ImagePickerDialog.razor for cover image selection

**Complexity:** 5 points
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `ImagePickerDialog.razor` modal component
- [ ] Display image cards in thumbnail grid
- [ ] Load images from app-managed sources (project/folder images)
- [ ] Implement single-select behavior (border highlight)
- [ ] Add Ok button to return selected image path
- [ ] Add Cancel button to discard selection
- [ ] Integrate into `ImportResourcePanel` "Set Cover" button
- [ ] Pass selected image path to import method

#### Changes Made

{To be updated after implementation}

---

### Step 3: Add the Import tab to Resources.razor

**Complexity:** 3 points
**Status:** [ ] Not Started

#### Tasks

- [ ] Add new `MudTabPanel` labeled "Import" to Resources.razor
- [ ] Wire up `ImportResourcePanel` component
- [ ] Trigger scan on tab activation
- [ ] Subscribe to `ResourcesChangedEventArgs` event
- [ ] Refresh resource tabs after successful import

#### Changes Made

{To be updated after implementation}

---

## Progress Tracking

| Step | Status | Complexity | Notes       |
| ---- | ------ | ---------- | ----------- |
| 1    | [ ]    | 5          | Not started |
| 2    | [ ]    | 5          | Not started |
| 3    | [ ]    | 3          | Not started |

---

## Issues & Resolutions

{To be documented as issues arise}

---

## Commit Checkpoints

- [ ] After Step 1 complete - Import panel with file selection and metadata form
- [ ] After Step 2 complete - Image picker dialog integrated
- [ ] After Step 3 complete - Import tab added to Resources page

---

## Design Notes

### UI Conventions to Follow

- Use `Variant.Text` for form controls (default)
- Use `Variant.Outlined` only for emphasis (primary actions)
- Children in layout slots render flush (no root `MudPaper`/`pa-*`)
- Use spacing tokens from `site.css` (no magic numbers)
- Simple action buttons use `.send-to-btn` style
- Subscribe to events via `EventService.cs` (pub/sub pattern)

### Component Structure

**ImportResourcePanel.razor:**

- File list with grouping by type
- Checkbox selection for batch import
- Metadata form with MudBlazor controls
- Import action button

**ImagePickerDialog.razor:**

- `MudDialog` wrapper
- Grid layout for image thumbnails
- Single-select state management
- Ok/Cancel actions

---

## Testing Notes

### Manual Testing Plan

After implementation, verify:

**Step 1:**

- Untracked files display correctly grouped by type
- Active/Inactive badges show based on file location
- Checkboxes allow single and multiple selection
- Metadata form fields work correctly
- Import button creates resources in database
- Success/error messages display

**Step 2:**

- Image picker dialog opens from "Set Cover" button
- Images load and display as thumbnails
- Single-select behavior works (border highlight)
- Ok returns selected image path
- Cancel discards selection

**Step 3:**

- Import tab appears on Resources page
- Tab activation triggers file scan
- Import completes and refreshes other tabs
- ResourcesChangedEventArgs event fires correctly
