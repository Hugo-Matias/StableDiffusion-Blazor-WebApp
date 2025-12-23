# Fragment Condition Checking - Implementation Guide

## Overview

This document explains the **three-tier solution** implemented to improve fragment condition checking:

1. **Runtime Validation** - Validates conditions during fragment rendering
2. **Startup Validation** - Scans all fragments at application startup (Development mode)
3. **Helper Functions** - Scriban template helpers for consistent condition generation

---

## **Tier 1: Runtime Validation**

### **`FragmentConditionValidator` Service**

Validates fragment conditions during workflow composition to catch issues immediately.

#### **Features:**

? **Case sensitivity validation** - Ensures snake_case for fragment IDs  
? **Format validation** - Checks `fragment_id.Property` pattern  
? **Property casing** - Validates PascalCase for properties (e.g., `IsActive`)  
? **Runtime logging** - Warns about malformed conditions during generation

#### **Example Usage:**

```csharp
// Injected into WorkflowService
var errors = _conditionValidator.ValidateConditions(fragmentId, conditions);
if (errors.Count > 0)
{
    _conditionValidator.LogValidationErrors(fragmentId, errors);
}
```

#### **Sample Output:**

```
[WARN] Fragment 'detailer-core.sbn' has 1 condition validation errors:
  - Condition 'Detailer.IsActive' should use snake_case (got 'Detailer')
```

---

## **Tier 2: Startup Validation**

### **`FragmentConditionGenerator` Service**

Scans all fragment files at application startup to detect condition issues proactively.

#### **Features:**

? **Auto-detection** - Identifies missing conditions in optional fragments  
? **Scoped fragment analysis** - Detects scoped loaders needing conditional checks  
? **Core fragment identification** - Skips validation for always-active fragments  
? **Suggested fixes** - Provides code templates to fix issues  
? **Development-only** - Runs only in Development environment

#### **Fragment Types Detected:**

| Type | Description | Example |
|------|-------------|---------|
| **Core** | Always active, no condition needed | `prompts.sbn`, `save.sbn` |
| **Optional** | Requires `IsActive` condition | `seed_vr2.sbn`, `detailer.sbn` |
| **Scoped** | Uses conditional conditions based on scope | `load-diffusion-w-prompts.sbn` |

#### **Sample Startup Report:**

```
[INFO] ? All fragment conditions are valid
```

or

```
[WARN] Fragment condition validation found 2 issues:

Fragment: upscale-seedvr2.sbn
  ID: upscale_seedvr2
  Issues:
    - Condition uses wrong case: 'SeedVR2' should be 'seedvr2'
  Suggested Condition: "upscale_seedvr2.IsActive"
  Suggested Fix:
    "conditions": {
      "required": ["upscale_seedvr2.IsActive"]
    }
```

---

## **Tier 3: Helper Functions**

### **Scriban Condition Helpers** (`utils/condition-helpers.sbn`)

Provides reusable functions for generating standard conditions (for future use when Scriban supports includes).

#### **Available Functions:**

```scriban
{{ fragment_condition "detailer" }}
? "conditions": { "required": ["detailer.IsActive"] }

{{ scoped_condition scope "detailer" }}
? Only adds condition if scope contains "detailer"

{{ multi_condition ["upscale", "detailer"] }}
? "conditions": { "required": ["upscale.IsActive", "detailer.IsActive"] }

{{ exclude_if "seed_vr2" }}
? "conditions": { "excluded_if": ["seed_vr2.IsActive"] }

{{ auto_condition fragment_file }}
? Auto-generates condition from filename
```

---

## **Condition Types**

Fragments support **two types of conditions**:

### **Type 1: Fragment Activation Conditions**

Checks if an optional fragment is enabled via UI toggle:

```json
"conditions": {
  "required": ["fragment_id.IsActive"]
}
```

**Use for:**

- Optional UI features (upscale, detailer, seedvr2, etc.)
- User-controlled fragment activation

### **Type 2: Parameter-Based Conditions**

Checks a boolean parameter value from workflow template:

```json
"conditions": {
  "required": ["parameter_name"]
}
```

**Use for:**

- Conditional rendering based on template logic
- Boolean flags passed from template parameters
- Workflow-specific conditional features

**Example:**

```scriban
// Template parameter controls which output to use
{
  "id": "concat_preview",
  "fragment": "concat-preview.sbn",
  "parameters": {
    "append_preview": {{ append_preview ?? false | json }}
  }
}

// Fragment only renders if append_preview is true
#meta
{
  "conditions": {
    "required": ["append_preview"]
  }
}
#end
```

---

## **Best Practices**

### **1. Use Consistent Naming**

```
? CORRECT                      ? WRONG
fragment_id.IsActive           Fragment_ID.IsActive
detailer.IsActive              Detailer.IsActive
seed_vr2.IsActive              SeedVR2.IsActive
```

### **2. Scoped Fragments Pattern**

For reusable fragments like loaders:

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

**Key Points:**
- Use parent fragment ID in condition (e.g., `detailer`, not `loader_detailer`)
- Ensures loader and core fragments activate/deactivate together
- Allows fragment reuse across contexts

### **3. Core vs Optional Fragments**

**Core Fragments** (no condition needed):
- `prompts.sbn`
- `empty-latent.sbn`
- `sampler.sbn`
- `save.sbn`
- `vae-decode.sbn`

**Optional Fragments** (require condition):
- `upscale-seedvr2.sbn`
- `detailer-core.sbn`
- `conditioning-variation.sbn`
- `seed-variance-enhancer.sbn`

---

## **Debugging Workflow**

### **Step 1: Check Startup Logs**

Run the app in Development mode:

```
[INFO] Pre-compiled 15 workflow templates and 42 fragments
[INFO] ? All fragment conditions are valid
```

### **Step 2: Check Runtime Logs**

Enable Debug logging for WorkflowService:

```json
{
  "Logging": {
    "LogLevel": {
      "BlazorWebApp.Services.WorkflowService": "Debug"
    }
  }
}
```

Output:

```
[DEBUG] Processing active fragment 'seed_vr2' (file: 'upscale-seedvr2.sbn')
[DEBUG] Required condition 'seed_vr2.IsActive' satisfied
```

### **Step 3: Validate Specific Fragment**

Use `FragmentConditionValidator` directly:

```csharp
var validator = new FragmentConditionValidator(logger);
var errors = validator.ValidateConditions("detailer", conditions);
foreach (var error in errors)
{
    Console.WriteLine($"  - {error}");
}
```

---

## **Migration Guide**

### **Updating Existing Fragments**

1. **Identify the fragment ID** from filename:
   ```
   detailer-core.sbn ? detailer_core
   ```

2. **Update condition to snake_case**:
   ```diff
   - "required": ["Detailer.IsActive"]
   + "required": ["detailer.IsActive"]
   ```

3. **For scoped fragments**, ensure parent ID is used:
   ```scriban
   {{~ if scope && scope | string.contains "detailer" ~}},
   "conditions": {
     "required": ["detailer.IsActive"]  // Parent, not "loader_detailer"
   }
   {{~ end ~}}
   ```

### **Adding New Fragments**

1. **Choose fragment type**:
   - Core (always active) ? No condition
   - Optional ? Add condition
   - Scoped ? Add conditional condition

2. **Use standard pattern**:

   **Optional Fragment:**
   ```json
   #meta
   {
     "outputs": { ... },
     "conditions": {
       "required": ["my_fragment.IsActive"]
     }
   }
   #end
   ```

   **Scoped Fragment:**
   ```scriban
   #meta
   {
     "outputs": { ... }
     {{~ if scope && scope | string_contains "parent" ~}},
     "conditions": {
       "required": ["parent.IsActive"]
     }
     {{~ end ~}}
   }
   #end
   ```

3. **Test both states**:
   - Active: Verify nodes render in payload
   - Inactive: Verify nodes excluded from payload

---

## **API Reference**

### **FragmentConditionValidator**

```csharp
// Validate conditions
List<string> ValidateConditions(string fragmentId, Dictionary<string, JsonElement>? conditions)

// Normalize filename to ID
static string NormalizeFragmentId(string fragmentFile)

// Generate standard condition
Dictionary<string, object> GenerateCondition(string fragmentId, bool isRequired = true)

// Generate scoped condition
Dictionary<string, object>? GenerateScopedCondition(string? scope, string parentFragmentId)
```

### **FragmentConditionGenerator**

```csharp
// Scan and validate all fragments
FragmentConditionReport GenerateAndValidate()

// Get human-readable report
string FragmentConditionReport.GenerateReport()
```

---

## **Testing Checklist**

- [ ] Startup validation passes (no warnings)
- [ ] All optional fragments have conditions
- [ ] Scoped fragments use parent fragment ID
- [ ] Conditions use snake_case format
- [ ] Properties use PascalCase (IsActive)
- [ ] Active fragments render in payload
- [ ] Inactive fragments excluded from payload
- [ ] No case mismatches in logs

---

## **Future Enhancements**

### **Possible Improvements:**

1. **Auto-fix mode** - Automatically update fragment files with correct conditions
2. **VS Code extension** - Real-time validation in editor
3. **Condition intellisense** - Auto-complete for condition paths
4. **Dependency tracking** - Validate fragment references in conditions
5. **Performance metrics** - Track condition evaluation overhead

---

## **Related Documentation**

- [`FRAGMENT_CONVENTIONS.md`](./FRAGMENT_CONVENTIONS.md) - Fragment naming and ID conventions
- [`TEMPLATE_GUIDE.md`](./TEMPLATE_GUIDE.md) - Workflow template guide
- [`FragmentKeys.cs`](../Models/FragmentKeys.cs) - Centralized fragment constant registry
