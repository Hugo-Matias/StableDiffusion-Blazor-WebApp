# Fragment Condition Types - Reference Guide

## Overview

Fragment conditions control whether a fragment renders in the final workflow payload. There are **two distinct types** of conditions, each serving different purposes.

---

## **Type 1: Fragment Activation Conditions**

### **Purpose**
Control rendering based on **user-toggled fragment activation** in the UI.

### **Format**
```json
"conditions": {
  "required": ["fragment_id.IsActive"]
}
```

### **How It Works**

1. **User toggles fragment** in UI (e.g., enables "SeedVR2 Upscale")
2. **Fragment parameters created** with `IsActive = true`
3. **Condition evaluates** `parameters["seed_vr2"]["IsActive"]` ? `true`
4. **Fragment renders** in workflow payload

### **Example: Optional SeedVR2 Upscale**

**Fragment:** `upscale-seedvr2.sbn`

```scriban
#meta
{
  "outputs": {
    "image_output": { "node": "seedvr2_upscaler", "index": 0 }
  },
  "conditions": {
    "required": ["seed_vr2.IsActive"]
  }
}
#end
```

**User Action:**
- Enable "SeedVR2" in UI ? Fragment renders
- Disable "SeedVR2" ? Fragment skipped

**Evaluation Path:**
```
parameters.Fragments["seed_vr2"].IsActive
? Check GenerationParameters.Fragments dictionary
? Look up "seed_vr2" key
? Check IsActive property
? true = render, false = skip
```

---

## **Type 2: Parameter-Based Conditions**

### **Purpose**
Control rendering based on **boolean parameter values** from the workflow template.

### **Format**
```json
"conditions": {
  "required": ["parameter_name"]
}
```

### **How It Works**

1. **Template defines parameter** (e.g., `append_preview: true`)
2. **Parameter passed to fragment** via pipeline step
3. **Condition evaluates** `parameters["append_preview"]` ? `true`
4. **Fragment renders** in workflow payload

### **Example: Conditional Preview Concatenation**

**Fragment:** `concat-preview.sbn`

```scriban
#meta
{
  "outputs": {
    "preview_concat": { "node": "concat_final", "index": 0 }
  },
  "conditions": {
    "required": ["append_preview"]
  }
}
#end
```

**Template:** `pose2vid-steadydancer.sbn`

```json
{
  "id": "concat_preview",
  "fragment": "concat-preview.sbn",
  "parameters": {
    "append_preview": {{ append_preview ?? false | json }}
  }
},
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
```

**Evaluation Path:**
```
parameters["append_preview"]
? Check flattened parameter dictionary
? Look up "append_preview" key (no nesting)
? Check boolean value
? true = render, false = skip
```

---

## **Comparison Table**

| Aspect | Fragment Activation | Parameter-Based |
|--------|---------------------|-----------------|
| **Format** | `fragment_id.IsActive` | `parameter_name` |
| **Source** | User UI toggles | Template parameters |
| **Nesting** | Two-level (fragment.property) | Single-level (parameter) |
| **Example** | `seed_vr2.IsActive` | `append_preview` |
| **Use Case** | Optional UI features | Template logic control |
| **Casing** | `snake_case.PascalCase` | `snake_case` |

---

## **Validation Rules**

### **Fragment Activation Conditions**

? **Valid:**
```json
"required": ["seed_vr2.IsActive"]
"required": ["detailer.IsActive"]
"required": ["frame_interpolation.IsActive"]
```

? **Invalid:**
```json
"required": ["SeedVR2.IsActive"]        // Wrong case
"required": ["seed-vr2.IsActive"]       // Dash instead of underscore
"required": ["seed_vr2.isActive"]       // Property not PascalCase
```

### **Parameter-Based Conditions**

? **Valid:**
```json
"required": ["append_preview"]
"required": ["use_refiner"]
"required": ["apply_watermark"]
```

? **Invalid:**
```json
"required": ["AppendPreview"]           // Should be snake_case
"required": ["append-preview"]          // Dash instead of underscore
"required": ["APPEND_PREVIEW"]          // All caps
```

---

## **When to Use Each Type**

### **Use Fragment Activation When:**

- ? Feature is **optional and user-controlled**
- ? Has a **UI toggle** or form component
- ? Stored in `GenerationParameters.Fragments`
- ? Examples: Upscale, Detailer, SeedVR2, LLM Enhancement

### **Use Parameter-Based When:**

- ? Controlled by **template logic**, not UI
- ? Boolean flag in **workflow template**
- ? Passed via pipeline step parameters
- ? Examples: `append_preview`, `use_controlnet`, `enable_tiling`

---

## **Complex Conditions**

### **Multiple Conditions (AND Logic)**

```json
"conditions": {
  "required": ["upscale.IsActive", "use_refiner"]
}
```

**Evaluation:**
- Both `upscale.IsActive` AND `use_refiner` must be `true`
- Fragment renders only if **all** conditions satisfied

### **Exclusion Conditions (NOT Logic)**

```json
"conditions": {
  "excluded_if": ["disable_enhancement"]
}
```

**Evaluation:**
- Fragment skipped if `disable_enhancement` is `true`
- Opposite of `required`

### **Conditional Conditions (Scoped Fragments)**

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

**Evaluation:**
- Condition only added if `scope` contains `"detailer"`
- Allows same fragment to be used in multiple contexts
- Main context: No condition (always renders)
- Detailer context: Requires `detailer.IsActive`

---

## **Debugging Conditions**

### **Enable Debug Logging**

```json
{
  "Logging": {
    "LogLevel": {
      "BlazorWebApp.Services.WorkflowService": "Debug"
    }
  }
}
```

### **Log Output Examples**

**Fragment Activation Condition:**
```
[DEBUG] Processing active fragment 'seed_vr2' (file: 'upscale-seedvr2.sbn')
[DEBUG] Required condition 'seed_vr2.IsActive' satisfied
```

**Parameter-Based Condition:**
```
[DEBUG] Processing fragment 'concat_preview' (file: 'concat-preview.sbn')
[DEBUG] Required condition 'append_preview' satisfied
```

**Condition Failed:**
```
[DEBUG] Fragment excluded: required condition 'detailer.IsActive' evaluated to false
```

### **Inspect Parameters Dictionary**

Use debugger to inspect:

**Fragment Activation:**
```csharp
parameters["seed_vr2"]["IsActive"] = true
```

**Parameter-Based:**
```csharp
parameters["append_preview"] = true
```

---

## **Common Mistakes**

### **1. Using .IsActive for Parameters**

? **Wrong:**
```json
"conditions": {
  "required": ["append_preview.IsActive"]
}
```

? **Correct:**
```json
"conditions": {
  "required": ["append_preview"]
}
```

### **2. Wrong Casing**

? **Wrong:**
```json
"conditions": {
  "required": ["AppendPreview"]  // Parameter should be snake_case
}
```

? **Correct:**
```json
"conditions": {
  "required": ["append_preview"]
}
```

### **3. Confusing Condition Types**

**Question:** Should I use `concat_preview.IsActive` or `append_preview`?

**Answer:**
- If it's a **UI toggle** ? `concat_preview.IsActive`
- If it's a **template parameter** ? `append_preview`

---

## **Migration Checklist**

When migrating fragments:

- [ ] Identify condition type (activation vs parameter)
- [ ] Check if fragment has UI component
- [ ] Verify parameter source (UI or template)
- [ ] Validate casing (snake_case for both, PascalCase for .IsActive)
- [ ] Test both true/false cases
- [ ] Check logs for condition evaluation

---

## **Examples from Codebase**

### **Fragment Activation Examples**

```
seed_vr2.IsActive          ? SeedVR2 upscale feature
detailer.IsActive          ? Face detailer feature
upscale.IsActive           ? General upscale feature
frame_interpolation.IsActive ? Frame interpolation
```

### **Parameter-Based Examples**

```
append_preview             ? Concatenate preview images
use_refiner               ? Enable refiner pass
apply_controlnet          ? Apply ControlNet
enable_tiling             ? Enable tiling mode
```

---

## **Related Documentation**

- [Fragment Conventions](./FRAGMENT_CONVENTIONS.md) - Naming and structure
- [Condition Checking Guide](./CONDITION_CHECKING_GUIDE.md) - Implementation details
- [Migration Plan](./FRAGMENT_MIGRATION_PLAN.md) - Migration tracking
- [Scoped Condition Fix](./SCOPED_CONDITION_FIX.md) - Scriban syntax fix
