---
name: comfyui-node-probing
description: 'Probe live ComfyUI object_info schema before implementing a node, fragment, workflow input binding, or backend option lookup. Use when adding a ComfyUI node, validating input key names, COMBO options, numeric ranges, output indexes, or debugging schema mismatches.'
argument-hint: 'ComfyUI node type and intended integration surface'
---

# ComfyUI Node Probing

Use this skill before writing code for any new or changed ComfyUI node integration.

## When To Use

- Implement a new ComfyUI node
- Add or change a fragment that wraps a specific node
- Validate `sampler_name`, `scheduler`, loader options, or other COMBO values
- Debug a workflow that fails because an input key, output index, or default value was assumed incorrectly

## Procedure

1. Identify the exact ComfyUI node type name from the workflow JSON or failing payload. Do not guess aliases.
2. Probe the running ComfyUI instance before coding.
3. Create a temporary `.ps1` script for the probe instead of pasting multi-step PowerShell directly into the terminal.
4. Prefer this probe first:

   ```powershell
   Invoke-RestMethod "http://localhost:8188/object_info/<NodeTypeName>" | ConvertTo-Json -Depth 10
   ```

5. If the endpoint returns duplicate keys or malformed output for normal JSON conversion, fall back to:

   ```powershell
   Invoke-WebRequest "http://localhost:8188/object_info/<NodeTypeName>" | ConvertFrom-Json -AsHashTable
   ```

6. Treat `{}` or a missing node as a blocker. Surface that before writing code.
7. Capture the exact details that matter to the app:
   - input key names such as `image` vs `images`
   - COMBO option values
   - integer and float min, max, and default values
   - output index positions
8. Map the verified schema to the owning app surface:
   - `BlazorWebApp/Workflows/Fragments/` for node-group behavior
   - `BlazorWebApp/Workflows/Templates/` for workflow-level composition
   - `BlazorWebApp/Services/ComfyUIService.cs` for option discovery or capability lookup
   - `BlazorWebApp/Components/Shared/Generation/Fragments/` if the node needs user-facing controls
9. Reuse existing fragments and UI components when the parameter shape already matches.
10. Validate with the narrowest relevant build or workflow test after the first code change.

## Guardrails

- Never hardcode COMBO options from docs or examples when the live node can be probed.
- Never assume output positions.
- If the node introduces user-facing controls, follow the workflow UI and design-language skills.
- If the change crosses persistence or events, follow the matching repo skills instead of inventing a new pattern.

## Key Anchors

- `../../copilot-instructions.md`
- `../../../BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`
- `../../../BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`
- `../../../BlazorWebApp/Services/ComfyUIService.cs`
- `../../../BlazorWebApp/Workflows/Fragments/`
- `../../../BlazorWebApp/Workflows/Templates/`