# ComfyUI Blazor Cleanup Nodes

Small ComfyUI node pack used by Blazor cleanup indexing.

## Install

Copy this folder into your ComfyUI `custom_nodes` directory and restart ComfyUI:

```powershell
Copy-Item -Recurse -Force .\Utils\ComfyUI-BlazorCleanup C:\AI\ComfyUI\custom_nodes\ComfyUI-BlazorCleanup
```

Or run the helper script from the repository root:

```powershell
.\Utils\ComfyUI-BlazorCleanup\install.ps1 -ComfyUIPath C:\AI\ComfyUI
```

After restart, verify the nodes:

```powershell
.\Utils\ComfyUI-BlazorCleanup\probe.ps1
```

## Nodes

- `BlazorCleanupImageEmbedding`: returns JSON text with `modelKey`, `dimensions`, and `embedding`.
- `BlazorCleanupImageScore`: returns JSON text with `modelKey`, `scoreName`, `score`, `minScore`, and `maxScore`.

Both nodes are output nodes and put their JSON in Comfy history `text`, which the Blazor app can already read.

## Current Model Behavior

The first implementation is dependency-free and deterministic:

- Embeddings use multi-scale image statistics and are useful for visual cleanup grouping by color, composition, and rough appearance.
- Scores use contrast, exposure, sharpness, and saturation as a practical quality heuristic.

This gives the app a working Comfy-backed cleanup runtime without extra model downloads. A later version can add CLIP/SigLIP/aesthetic model backends behind the same node names and JSON contract.
