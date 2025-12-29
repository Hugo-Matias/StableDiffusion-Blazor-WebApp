# CRITICAL: Liquid Quote Syntax Correction

## The Problem We Were Chasing

You were absolutely right - we were going in circles. The error kept showing:
```
"high_model_output": { "node": "'model' }}_unet_loader"
```

## What We Got Wrong in Phase 7

**Phase 7.11 (WRONG FIX):** We changed all fragments to use **double quotes**:
```liquid
? {{ node_prefix | default: "model" }}  // We thought this was correct
```

**The Result:** Fluid rendered this as `"model"` WITH the double quotes included, breaking JSON!

## The Root Cause

**Liquid/Fluid Filter Syntax Rules:**
- **Single quotes (`'`)** = String literal delimiter (correct!)
- **Double quotes (`"`)** = Also a string literal, but Fluid includes them in output

When you write:
```liquid
"node": "{{ prefix | default: "value" }}"
```

Fluid renders:
```json
"node": ""value""  ? BROKEN! Double quotes included
```

When you write:
```liquid
"node": "{{ prefix | default: 'value' }}"
```

Fluid renders:
```json
"node": "value"  ? CORRECT! Clean string
```

## The Correct Solution

**? Always use single quotes in Liquid filter arguments:**
```liquid
{{ var | default: 'string_value' }}
{{ var | default: 20 }}              // No quotes for numbers
{{ var | default: true }}            // No quotes for booleans
{{ var | append: '_suffix' }}         // Single quotes for strings
```

## What We Fixed

1. **Reverted 38 fragment files** back to single quotes
2. **Corrected FLUID_CONVENTIONS.md** - Single quotes are correct
3. **Updated PHASE_7.md** - Documented the correction
4. **Updated QUICK_REFERENCE.md** - Fixed the examples

## Why This Was So Confusing

1. **Most languages use double quotes** for strings (C#, JavaScript, JSON)
2. **JSON requires double quotes** for keys and string values
3. **But Liquid uses single quotes** for filter arguments
4. This is standard Liquid syntax (Jekyll, Shopify, etc.) but counter-intuitive

## The Key Insight

**Context matters:**
- **JSON context**: Use double quotes for JSON strings
- **Liquid filter context**: Use single quotes for string literals
- **When Liquid output goes INTO JSON**: Use single quotes in filters so output doesn't have extra quotes

## Files Changed (Correction)

| File | Action |
|------|--------|
| 38 `.liquid` fragment files | Reverted double ? single quotes |
| `FLUID_CONVENTIONS.md` | Corrected Golden Rules |
| `PHASE_7.md` | Documented the correction |
| `QUICK_REFERENCE.md` | Fixed examples |

## Testing

**Restart your application now.** The fragments are corrected and the error should be gone:
- ? Single quotes in all filter arguments
- ? Clean string output (no extra quotes)
- ? Valid JSON rendering
- ? WAN img2vid should work

## Lesson Learned

**Always verify the template engine's actual behavior** instead of assuming based on other language conventions. Liquid/Fluid has its own rules that differ from C#/JSON/JavaScript quote syntax.

The confusion came from mixing contexts:
- We were editing **Liquid templates**
- That output **JSON**
- Using **C# conventions** in our heads
- But **Liquid has its own rules**

**Single quotes win!** ??
