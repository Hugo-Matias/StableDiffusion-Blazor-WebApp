# Liquid Template Quick Reference

## ?? CRITICAL RULES - Never Break These

### 1. Always Use Single Quotes in Filter Arguments
```liquid
? {{ var | default: 'value' }}
? {{ var | default: "value" }}  // Includes quotes in output!
```

### 2. Never Use `| json` on Object Keys
```liquid
? "{{ key }}": { ... }
? {{ key | json }}: { ... }  // Double-encodes!
```

### 3. Always Use `| json` on String Values
```liquid
? "name": {{ var | json }}
? "name": {{ var }}  // Missing quotes!
```

### 4. Never Use `| json` in Arrays
```liquid
? ["{{ node_id }}", 0]
? [{{ node_id | json }}, 0]  // Double-encodes!
```

---

## Common Patterns

### Object with Dynamic Key
```liquid
"{{ scope | default: "" }}{{ node_id }}": {
  "class_type": "NodeType",
  "inputs": {
    "value": {{ param | json }}
  }
}
```

### Conditional Content
```liquid
{% if feature_enabled %}
"feature_node": {
  "inputs": { ... }
},
{% endif %}
```

### Loop Over Collection
```liquid
{% for item in items %}
"node_{{ forloop.index0 }}": {
  "name": {{ item.name | json }}
}{% unless forloop.last %},{% endunless %}
{% endfor %}
```

### String Concatenation
```liquid
{{ prefix | default: "base" | append: "_suffix" }}
```

### Node Reference
```liquid
"model": {% get_ref "model_output" %}
```

### Assign Variable for Reuse
```liquid
{% assign node_id = scope | append: "loader" %}
"{{ node_id }}": {
  "inputs": {
    "model": ["{{ node_id }}", 0]
  }
}
```

---

## Meta Block Template
```liquid
{% meta %}
{
  "outputs": {
    "output_key": { "node": "node_id", "index": 0 }
  },
  "ui": {
    "title": "Fragment Title"
  }
}
{% endmeta %}

{# Fragment body starts here #}
"node_id": {
  "class_type": "NodeClassName",
  "inputs": { ... }
}
```

---

## Troubleshooting

### Error: Invalid JSON - quotes in wrong place
**Cause:** Single quotes in filter arguments  
**Fix:** Change `'value'` to `"value"`

### Error: Double-encoded string
**Cause:** `| json` on object key or array element  
**Fix:** Remove `| json`, keep quotes in template

### Error: Missing quotes in JSON
**Cause:** Forgot `| json` on string value  
**Fix:** Add `| json` filter

---

## File Types

| Extension | Purpose | Quote Context |
|-----------|---------|---------------|
| `.workflow` | Workflow definitions | JSON context - use JSON rules |
| `.liquid` | Fragment templates | Liquid context - use Liquid rules |

---

**Full documentation:** See `FLUID_CONVENTIONS.md`
