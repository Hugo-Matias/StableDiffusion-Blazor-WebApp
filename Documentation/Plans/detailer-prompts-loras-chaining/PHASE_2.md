# Phase 2 - DetailerForm UI (Single Pass)

## Status
**Phase:** 2
**Build Status:** (not yet run)

---

## Objective

Add a tabbed prompt section (Positive/Negative, `AutoGrow`), a detailer-scoped LoRA panel, and split Copy/Clear buttons to `DetailerForm.razor`. Single pass only.

---

## Execution Checklist

### Step 1: Tabbed Prompts with AutoGrow
**Status:** [~] In Progress
- Add MudTabs block at top with Positive and Negative MudTextFields (`AutoGrow="true" MaxLines="12"`).
- Bind to `detailer_prompt` / `detailer_negative_prompt` on the fragment.
- Helper text "Leave blank to use main prompt".

### Step 2: Copy / Clear Prompt Buttons
- `Copy Prompts from Main` (reads `prompts` fragment positive/negative).
- `Clear Prompts` resets both to empty.

### Step 3: Detailer LoRA Panel
- `<LoraForm Loras="@_paramService.Current.DetailerLoras" ...>`.
- Dual-model awareness via workflow base.

### Step 4: Copy / Clear LoRA Buttons
- `Copy LoRAs from Main` clones each `new Lora(main)` (snapshot).
- `Clear LoRAs` empties `DetailerLoras`.
