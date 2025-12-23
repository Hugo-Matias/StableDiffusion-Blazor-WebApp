# Fragment Naming and ID Conventions

## Fragment ID Generation

Fragment IDs are automatically derived from the fragment filename:
- **Filename**: `upscale-seedvr2.sbn`
- **Derived ID**: `upscale_seedvr2` (dashes ? underscores, extension removed)

## Fragment Storage in GenerationParameters

Fragments are stored in the `GenerationParameters.Fragments` dictionary using their derived ID:

```csharp
parameters.Fragments["seed_vr2"]         // ? Correct
parameters.Fragments["SeedVR2"]          // ? Wrong - case mismatch
parameters.Fragments["upscale_seedvr2"]  // ? Also correct if using full filename
```

## Fragment Conditions

Fragment conditions in `#meta` blocks can be **two types**:

### **1. Fragment Activation Conditions**

Checks if an optional fragment is enabled in the UI:

```json
// ? CORRECT
"conditions": {
  "required": ["seed_vr2.IsActive"]
}

// ? WRONG - case mismatch
"conditions": {
  "required": ["SeedVR2.IsActive"]
}
```

### **2. Parameter-Based Conditions**

Checks a boolean parameter value from the workflow template:

```json
// ? CORRECT - checks if 'append_preview' parameter is true
"conditions": {
  "required": ["append_preview"]
}

// ? WRONG - parameters should be snake_case
"conditions": {
  "required": ["AppendPreview"]
}
```

**When to use each:**
- **Fragment activation** (`fragment_id.IsActive`): For optional UI features the user can toggle
- **Parameter condition** (`parameter_name`): For conditional rendering based on template parameters

**Example:**
```scriban
// In workflow template
{
  "id": "video_save",
  "fragment": "save-video.sbn",
  "parameters": {
    {{~ if append_preview ~}}
    "image_ref": "preview_concat"
    {{~ else ~}}
    "image_ref": "image_output"
    {{~ end ~}}
  }
}

// In concat-preview.sbn fragment
#meta
{
  "conditions": {
    "required": ["append_preview"]  // Only render if parameter is true
  }
}
#end
```

### **Scoped Fragments and Conditional Conditions**

Some fragments are reusable with different scopes (e.g., `load-diffusion-w-prompts.sbn`). These can have **conditional condition checks** using Scriban:

```scriban
#meta
{
  "outputs": { ... }
  {{~ if scope && string_contains scope "detailer" ~}},
  "conditions": {
    "required": ["detailer.IsActive"]
  }
  {{~ end ~}}
}
#end
```

**Important**: The condition path must match the **parent fragment ID**, not the scoped loader ID:

```json
// In workflow template:
{
  "id": "loader_detailer",        // This is the fragment ID
  "fragment": "load-diffusion-w-prompts.sbn",
  "parameters": { "scope": "detailer_" }  // Scope for node names
},
{
  "id": "detailer",                // Parent fragment ID (used in condition)
  "fragment": "detailer-core.sbn"
}

// The loader uses condition: "detailer.IsActive" 
// NOT "loader_detailer.IsActive"
// The string_contains check matches "detailer" within "detailer_" scope
```

This ensures both the loader and core fragments are activated/deactivated together.

## Pipeline Step IDs

The `id` field in workflow templates is **required** and serves two purposes:

1. **Unique identification** when the same fragment is used multiple times
2. **Parameter lookup** in `GenerationParameters.Fragments`

```json
{
  "id": "seed_vr2",              // Used for parameter lookup
  "fragment": "upscale-seedvr2.sbn",
  "parameters": { ... }
}
```

### When to Use Custom IDs

Use custom IDs when you need multiple instances of the same fragment:

```json
// Example: Multiple refiners
{
  "id": "refiner_1",
  "fragment": "refiner-sampler.sbn",
  "parameters": { "refiner_strength": 0.3 }
},
{
  "id": "refiner_2",
  "fragment": "refiner-sampler.sbn",
  "parameters": { "refiner_strength": 0.5 }
}
```

## FragmentKeys Constants

Update `FragmentKeys.cs` when adding new fragments:

```csharp
public static class Fragments
{
    // Use snake_case matching the derived ID
    public const string SeedVR2 = "seed_vr2";
    public const string ConditioningVariation = "conditioning_variation";
    public const string SeedVarianceEnhancer = "seed_variance_enhancer";
}
```

## Debugging Fragment Activation

If a fragment doesn't render despite `IsActive = true`:

1. **Check fragment ID match**: Ensure `parameters.Fragments` key matches the condition path
2. **Check condition evaluation**: Review logs for "Fragment excluded" messages
3. **Verify fragment exists in Pipeline**: The workflow template must reference it
4. **Check parameter flattening**: Ensure nested parameters are properly flattened

### Debug Logging

The WorkflowService logs fragment processing:

```
[DEBUG] Processing active fragment 'seed_vr2' (file: 'upscale-seedvr2.sbn')
[DEBUG] Required condition 'seed_vr2.IsActive' satisfied
```

or if excluded:

```
[DEBUG] Skipping inactive fragment 'seed_vr2' (file: 'upscale-seedvr2.sbn')
[DEBUG] Fragment excluded: required condition 'seed_vr2.IsActive' evaluated to false
```

## Best Practices

1. **Always use snake_case** for fragment IDs and condition paths
2. **Keep filenames lowercase** with hyphens (e.g., `my-fragment.sbn`)
3. **Document custom IDs** in workflow templates with comments
4. **Test fragment activation** in the UI before deploying
5. **Use consistent naming** across FragmentKeys, template IDs, and condition paths
