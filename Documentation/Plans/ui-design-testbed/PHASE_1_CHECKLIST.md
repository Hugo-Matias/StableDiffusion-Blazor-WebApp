# Phase 1: Foundation - Implementation Checklist

## Overview

**Complexity:** 3 Fibonacci points  
**Deliverable:** Dev page registered, basic shell, token display tab, layout examples tab

## Goals

- Create development-only page accessible at `/dev/design-testbed`
- Register route with environment guard (dev only)
- Implement `TabbedPageShell` with initial tab structure
- Display all CSS custom properties from `:root` with computed values
- Show empty examples of each layout variant

## Prerequisites

- [x] Plan reviewed and approved
- [ ] Design language doc read (`04-UI-DESIGN-LANGUAGE.md`)
- [ ] Layout components reviewed (`Components/Layouts/`)
- [ ] Token system understood (`wwwroot/site.css`)

---

## Steps

### Step 1.1: Create Page Structure

**Files to create:**

- `BlazorWebApp/Pages/Dev/DesignTestbed.razor`
- `BlazorWebApp/Pages/Dev/DesignTestbed.razor.cs` (code-behind)
- `BlazorWebApp/Pages/Dev/DesignTestbed.razor.css` (scoped styles, initially empty)

**Requirements:**

- Route directive: `@page "/dev/design-testbed"`
- Page title: `@page:title "Design Test Bed"`
- Namespace: `BlazorWebApp.Pages.Dev`
- Environment check in `OnInitialized`: redirect to 404 if not Development

**Markup skeleton:**

```razor
@page "/dev/design-testbed"
@page:title "Design Test Bed"
@inject IWebHostEnvironment HostEnvironment
@inject NavigationManager Navigation

<TabbedPageShell @bind-ActivePanelIndex="_activeTabIndex">
    <MudTabPanel Text="Tokens & Variables">
        @* Step 1.3 content *@
    </MudTabPanel>

    <MudTabPanel Text="Layout System">
        @* Step 1.4 content *@
    </MudTabPanel>

    @* Additional tabs will be added in future phases *@
</TabbedPageShell>
```

**Code-behind:**

```csharp
protected override void OnInitialized()
{
    // Guard: only allow in Development environment
    if (!HostEnvironment.IsDevelopment())
    {
        Navigation.NavigateTo("/404", replace: true);
    }
}
```

**Success criteria:**

- [ ] Page loads at `/dev/design-testbed` in development
- [ ] Page returns 404 in non-development environments
- [ ] `TabbedPageShell` renders with two empty tabs

---

### Step 1.2: Add Navigation Link (Optional Dev Menu)

**Decision:** How should developers discover this page?

**Option A:** Add to NavBar with environment guard

```razor
@if (HostEnvironment.IsDevelopment())
{
    <MudNavLink Href="/dev/design-testbed" Icon="@Icons.Material.Filled.Palette">
        Design Test Bed
    </MudNavLink>
}
```

**Option B:** Require manual URL entry (keep hidden)

**Option C:** Add to bottom of existing admin/settings page

**Recommended:** Start with Option B (manual entry), add to NavBar in Phase 9 after validation.

**Success criteria:**

- [ ] Decision made and documented in phase notes

---

### Step 1.3: Token Display Tab

**Goal:** Display all CSS custom properties from `:root` with computed values

**Approach:**

1. Use JavaScript interop to get computed styles from `:root`
2. Filter for `--app-*` prefixed variables
3. Display in a table: Variable Name | Computed Value | Purpose

**Markup example:**

```razor
<MudTabPanel Text="Tokens & Variables">
    <ContentOnlyLayout>
        <MudText Typo="Typo.h5" GutterBottom>Global CSS Variables</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Source: <code>BlazorWebApp/wwwroot/site.css</code> (`:root` section)
        </MudText>

        <MudSimpleTable Dense Bordered>
            <thead>
                <tr>
                    <th>Variable</th>
                    <th>Value</th>
                    <th>Purpose</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var token in _tokens)
                {
                    <tr>
                        <td><code>@token.Name</code></td>
                        <td>@token.Value</td>
                        <td>@token.Description</td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </ContentOnlyLayout>
</MudTabPanel>
```

**Code-behind:**

```csharp
private List<TokenInfo> _tokens = new();

public class TokenInfo
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // Get computed styles via JS
        var computedValues = await JS.InvokeAsync<Dictionary<string, string>>(
            "getComputedTokens", "--app-"
        );

        _tokens = computedValues.Select(kvp => new TokenInfo
        {
            Name = kvp.Key,
            Value = kvp.Value,
            Description = GetTokenDescription(kvp.Key)
        }).ToList();

        StateHasChanged();
    }
}

private string GetTokenDescription(string tokenName)
{
    // Map token names to descriptions (from design doc comments)
    return tokenName switch
    {
        "--app-gutter-outer" => "Gap between page and window/navbar edges",
        "--app-gutter-inner" => "Gap between shell elements (tabs, panels)",
        "--app-sidebar-width" => "Sidebar width as percentage",
        "--app-sidebar-min" => "Sidebar min-width clamp",
        "--app-sidebar-max" => "Sidebar max-width clamp",
        "--app-sidebar-rail-width" => "Collapsed sidebar rail width",
        "--app-shell-max-width" => "Page max-width clamp",
        "--app-surface-radius" => "Corner radius for surfaces",
        "--app-surface-padding" => "Internal padding of surfaces",
        "--app-dialog-max-width" => "Dialog max-width clamp",
        "--app-dialog-padding" => "Dialog internal padding",
        "--app-dialog-radius" => "Dialog corner radius",
        _ => ""
    };
}
```

**JavaScript helper** (`wwwroot/js/design-testbed.js`):

```javascript
window.getComputedTokens = (prefix) => {
  const root = document.documentElement;
  const computed = getComputedStyle(root);
  const tokens = {};

  // Get all custom properties starting with prefix
  for (const prop of Array.from(document.styleSheets)
    .flatMap((sheet) => Array.from(sheet.cssRules || []))
    .filter((rule) => rule.selectorText === ":root")
    .flatMap((rule) => Array.from(rule.style))) {
    if (prop.startsWith(prefix)) {
      tokens[prop] = computed.getPropertyValue(prop).trim();
    }
  }

  return tokens;
};
```

**Alternative (simpler):** Hard-code the token list (manual but reliable):

```csharp
_tokens = new List<TokenInfo>
{
    new() { Name = "--app-gutter-outer", Value = "12px", Description = "Gap between page and window/navbar edges" },
    new() { Name = "--app-gutter-inner", Value = "8px", Description = "Gap between shell elements" },
    // ... etc
};
```

**Decision:** Use simpler hard-coded approach initially, add JS interop in Phase 9 if desired.

**Success criteria:**

- [ ] Token table displays all `--app-*` variables
- [ ] Values are accurate (match `site.css`)
- [ ] Descriptions are clear

---

### Step 1.4: Layout System Tab

**Goal:** Show empty examples of each layout variant side-by-side

**Markup example:**

```razor
<MudTabPanel Text="Layout System">
    <ContentOnlyLayout>
        <MudText Typo="Typo.h5" GutterBottom>Layout Variants</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Source: <code>Components/Layouts/</code> |
            <MudLink Href="/Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md#layout" Target="_blank">
                Design Doc: Layout
            </MudLink>
        </MudText>

        <MudStack Spacing="4">
            @* TwoColumnLayout example *@
            <MudPaper Elevation="0" Class="pa-4" Style="border: 2px dashed var(--mud-palette-divider);">
                <MudText Typo="Typo.h6" GutterBottom>TwoColumnLayout</MudText>
                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mb-2">
                    Use when: Settings/search/browse tree on left, main content on right
                </MudText>

                <div style="height: 200px; border: 1px solid var(--mud-palette-divider); border-radius: 4px;">
                    <TwoColumnLayout>
                        <Sidebar>
                            <MudText>Sidebar content (filter/nav)</MudText>
                        </Sidebar>
                        <Content>
                            <MudText>Main content area</MudText>
                        </Content>
                    </TwoColumnLayout>
                </div>
            </MudPaper>

            @* TopbarLayout example *@
            <MudPaper Elevation="0" Class="pa-4" Style="border: 2px dashed var(--mud-palette-divider);">
                <MudText Typo="Typo.h6" GutterBottom>TopbarLayout</MudText>
                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mb-2">
                    Use when: Small filter/search cluster above single large content surface
                </MudText>

                <div style="height: 200px; border: 1px solid var(--mud-palette-divider); border-radius: 4px;">
                    <TopbarLayout>
                        <Topbar>
                            <MudText>Filter/search controls</MudText>
                        </Topbar>
                        <Content>
                            <MudText>Main content (e.g., gallery)</MudText>
                        </Content>
                    </TopbarLayout>
                </div>
            </MudPaper>

            @* ContentOnlyLayout example *@
            <MudPaper Elevation="0" Class="pa-4" Style="border: 2px dashed var(--mud-palette-divider);">
                <MudText Typo="Typo.h6" GutterBottom>ContentOnlyLayout</MudText>
                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mb-2">
                    Use when: Self-contained page, no sidebar/topbar needed
                </MudText>

                <div style="height: 200px; border: 1px solid var(--mud-palette-divider); border-radius: 4px;">
                    <ContentOnlyLayout>
                        <MudText>Single content surface filling the shell</MudText>
                    </ContentOnlyLayout>
                </div>
            </MudPaper>
        </MudStack>
    </ContentOnlyLayout>
</MudTabPanel>
```

**Success criteria:**

- [ ] All three layout variants displayed
- [ ] Each example shows the visual structure (dashed borders for clarity)
- [ ] Descriptions reference when to use each variant
- [ ] Link to design doc layout section

---

## Testing Checklist

### Functional Testing

- [ ] Navigate to `/dev/design-testbed` in dev environment → page loads
- [ ] Navigate to `/dev/design-testbed` in non-dev environment → 404
- [ ] Switch between "Tokens & Variables" and "Layout System" tabs → both render
- [ ] Token table displays all 12 `--app-*` variables with values
- [ ] Layout examples render without errors
- [ ] Clicking design doc link (if external) opens in new tab

### Visual Testing

- [ ] Page uses `TabbedPageShell` correctly (tabs centered, rounded)
- [ ] No console errors in browser dev tools
- [ ] Layout examples are visually distinct (borders help)
- [ ] Token table is readable (not cramped)

### Build Testing

- [ ] `dotnet build` succeeds
- [ ] No new warnings introduced
- [ ] Page compiles in both Debug and Release configurations

---

## Documentation Updates

### After Phase 1 Completion

- [ ] Create `PHASE_1.md` with:
  - Actual implementation decisions made
  - Deviations from plan (if any)
  - Screenshots of completed tabs
  - Known issues or future improvements
- [ ] Update `README.md` status to "Phase 1 Complete"
- [ ] Update `MAIN_PLAN.md` to mark Phase 1 as done

---

## Known Issues / Future Improvements

- Token values are hard-coded (Phase 9: add JS interop for live values)
- No navigation link in NavBar (add in Phase 9 after validation)
- Layout examples are empty (Phase 2-7 will populate with real pattern demos)
- No build-time guard for Release config (add warning in Phase 1 if time permits)

---

## Estimated Time

- Step 1.1: 30 min (page structure, environment guard)
- Step 1.2: 5 min (navigation decision)
- Step 1.3: 45 min (token display with hard-coded list)
- Step 1.4: 30 min (layout examples)
- Testing: 20 min

**Total: ~2 hours active work** (3 Fibonacci points = small-medium task)

---

## Ready to Start?

When executing:

1. Create files in order (Step 1.1 → 1.2 → 1.3 → 1.4)
2. Test after each step (incremental validation)
3. Commit after each successful test (safe checkpoints)
4. Update this checklist as you go (track progress)
5. Create `PHASE_1.md` after all steps complete

**Next phase:** Phase 2 - Form Controls Catalog (5 points)
