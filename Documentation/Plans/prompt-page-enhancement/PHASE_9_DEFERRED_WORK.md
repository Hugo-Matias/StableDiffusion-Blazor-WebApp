# Phase 9: Deferred Work - Autocomplete Enhancements

## Phase Info
**Status:** [ ] Not Started  
**Complexity:** TBD (will aggregate deferred items)  
**Related Plan:** [MAIN_PLAN.md](MAIN_PLAN.md)

---

## Overview

This document tracks work items deferred from earlier phases to be implemented in Phase 9.
Each item includes the original context and implementation details to preserve knowledge.

---

## Deferred Items

### Item 1: Wildcard Collection Preview Tooltip (from Phase 4, Step 6)

**Original Phase:** Phase 4 - Autocomplete Multi-Trigger System  
**Deferred Date:** Current Session  
**Reason for Deferral:** Nice-to-have feature, not critical for core wildcard functionality

#### Description
Add a hover preview tooltip for wildcard collections in the autocomplete dropdown.
When user hovers over a collection, show a preview of the first 5 entries with their weights.

#### User Story
As a user, when I hover over a wildcard collection in the dropdown, I want to see a preview
of what entries are in that collection so I can make an informed selection.

#### Tasks
- [ ] Add hover state detection for wildcard collections
- [ ] Load collection entries on hover (lazy load)
- [ ] Display tooltip with first 5 entries + weights
- [ ] Show total entry count
- [ ] Style tooltip consistently with app theme
- [ ] Test preview performance

#### Implementation Design

**State Variables:**
```csharp
private WildcardCollection? _hoveredCollection;
private bool _showPreview = false;
private System.Timers.Timer? _hoverTimer;
private const int HoverDelay = 300; // ms before showing preview
```

**Hover Detection:**
```csharp
private void OnCollectionMouseEnter(WildcardCollection collection)
{
    _hoveredCollection = collection;
    _hoverTimer?.Stop();
    _hoverTimer = new System.Timers.Timer(HoverDelay);
    _hoverTimer.Elapsed += (s, e) => 
    {
        _showPreview = true;
        InvokeAsync(StateHasChanged);
    };
    _hoverTimer.AutoReset = false;
    _hoverTimer.Start();
}

private void OnCollectionMouseLeave()
{
    _hoverTimer?.Stop();
    _hoverTimer?.Dispose();
    _showPreview = false;
    _hoveredCollection = null;
    StateHasChanged();
}
```

**UI Component:**
```razor
@if (_hoveredCollection != null && _showPreview)
{
    <MudPopover Open="true" 
                AnchorOrigin="Origin.CenterRight" 
                TransformOrigin="Origin.CenterLeft" 
                Class="wildcard-preview-tooltip">
        <MudPaper Class="pa-3" Elevation="4" Style="min-width: 200px; max-width: 350px;">
            <MudText Typo="Typo.subtitle2" Class="d-flex align-center">
                <MudIcon Icon="@Icons.Material.Filled.Casino" Size="Size.Small" Class="mr-2" />
                @_hoveredCollection.Name
            </MudText>
            <MudDivider Class="my-2" />
            <MudText Typo="Typo.caption" Style="opacity:0.7;">
                <strong>@_hoveredCollection.Entries.Count</strong> entries
            </MudText>
            <MudList Dense Clickable="false" Class="mt-2" Style="max-height: 150px; overflow-y: auto;">
                @foreach (var entry in _hoveredCollection.Entries.Take(5))
                {
                    <MudListItem Dense Style="padding: 2px 8px;">
                        <MudText Typo="Typo.caption">
                            • @entry.Value
                            @if (entry.Weight != 1.0)
                            {
                                <span style="opacity:0.5; margin-left: 4px;">
                                    (×@entry.Weight.ToString("F1"))
                                </span>
                            }
                        </MudText>
                    </MudListItem>
                }
                @if (_hoveredCollection.Entries.Count > 5)
                {
                    <MudListItem Dense Style="padding: 2px 8px;">
                        <MudText Typo="Typo.caption" Style="opacity:0.5; font-style: italic;">
                            +@(_hoveredCollection.Entries.Count - 5) more entries...
                        </MudText>
                    </MudListItem>
                }
            </MudList>
        </MudPaper>
    </MudPopover>
}
```

**Updated Collection List Item:**
```razor
<MudListItem Class="@(isSelected ? "selected-suggestion" : "")"
             OnClick="() => SelectWildcardCollection(collection)"
             @onmouseenter="() => OnCollectionMouseEnter(collection)"
             @onmouseleave="OnCollectionMouseLeave"
             Style="@(isSelected ? "background-color: var(--mud-palette-action-default-hover);" : "")">
    <!-- existing content -->
</MudListItem>
```

#### Success Criteria
- [ ] Hover triggers preview after 300ms delay
- [ ] Preview loads quickly (< 200ms after delay)
- [ ] Shows first 5 entries with weights
- [ ] Shows total entry count
- [ ] Weights displayed only when != 1.0
- [ ] No performance issues with rapid hover changes
- [ ] Tooltip positioned correctly (right of item)
- [ ] Tooltip doesn't block dropdown interaction
- [ ] Clean disposal of timer on component unmount

#### Performance Considerations
- Lazy load entries only when hovering
- Use debounce timer to prevent rapid loading
- Consider caching loaded collections
- Limit entries shown (5 max in tooltip)
- Dispose timer properly to prevent memory leaks

#### Accessibility
- Ensure tooltip content is accessible via keyboard
- Consider adding aria-describedby for screen readers
- Tooltip should not trap focus

#### CSS Styling (if needed)
```css
.wildcard-preview-tooltip {
    z-index: 1400; /* Above autocomplete dropdown */
}

.wildcard-preview-tooltip .mud-paper {
    border-left: 3px solid var(--mud-palette-tertiary);
}
```

#### Dependencies
- Phase 4 Step 5 (Selection Logic) - ? COMPLETED
- WildcardCollection model with Entries property - ? EXISTS

#### Estimated Complexity
**2 points** - Moderate UI work, performance considerations

---

### Item 2: [Future Deferred Item]

**Original Phase:** Phase X  
**Deferred Date:** TBD  
**Reason for Deferral:** TBD

{Template for future deferred items}

---

## Integration Notes

When implementing Phase 9:
1. Review all deferred items for relevance
2. Some items may no longer be needed based on user feedback
3. Prioritize based on user impact
4. Consider combining related items

---

## Related Documentation

- [MAIN_PLAN.md](MAIN_PLAN.md) - Overall project plan
- [PHASE_4.md](PHASE_4.md) - Source of Item 1
- [IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md) - General guidelines
