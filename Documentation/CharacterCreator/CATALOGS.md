# Character Creator Catalogs

Character Creator catalogs live in `BlazorWebApp/Data/CharacterCreator/` and are loaded at runtime. Edit the JSON files, then use the Catalog reload action on `/characters` to refresh regions, traits, prompt rules, and presets without rebuilding the app.

## Files

- `character_regions.json`: region buttons and grouping for the body map.
- `character_traits.json`: editable trait controls and option prompt text.
- `character_prompt_rules.json`: prompt profiles and negative guard templates.
- `character_presets.json`: reusable starter trait assignments.

Each file uses the same envelope shape:

```json
{
  "schemaVersion": 1,
  "catalogId": "character-traits",
  "displayName": "Character Traits",
  "items": []
}
```

## Adding Trait Options

Add options to an existing trait in `character_traits.json`:

```json
{
  "id": "eyes.color",
  "regionId": "eyes",
  "label": "Eye Color",
  "valueType": "option",
  "scope": "identity",
  "allowCustomValue": true,
  "options": [{ "id": "violet", "label": "Violet", "prompt": "violet eyes" }]
}
```

Trait ids must be unique inside the file. Region ids must match an item from `character_regions.json`.

## Validation

The `/characters` Catalog panel shows warnings for malformed files, duplicate ids, incorrect catalog ids, and missing required fields. When a file cannot be loaded, the app falls back to its built-in defaults for that catalog segment.
