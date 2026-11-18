# Phase 9 - Navigation, Polish, Events

## Status

**Phase:** 9 - COMPLETE
**Build Status:** Green | **Tests:** 92/92 Scheduler tests passing (1049/1067 overall; 18 pre-existing non-Scheduler failures documented below).

---

## Objective

Verify the Scheduler feature integrates cleanly with existing navigation, follows MudBlazor theming conventions, and has no event-subscription leaks. Most of this work was already satisfied as part of Phases 4-8; this phase audits and documents compliance.

## Verification

### Step 1 - `/scheduler` in main navigation

**Status:** [x] Delivered in Phase 5.

- `BlazorWebApp/Components/Shared/NavBar.razor` adds a top-level `<MudNavLink Href="/scheduler" Match=NavLinkMatch.Prefix>` with a `fa-calendar-check` icon beside the existing Prompts entry.
- Route registration handles `/scheduler` (Runs), `/scheduler/editor`, and `/scheduler/results` plus the `?jobId=<guid>` query param.

### Step 2 - Job event args

**Status:** [x] Delivered in Phase 4.

Five event-arg classes in `BlazorWebApp/Events/` follow the existing `EventArgs` pattern:

- `JobStartedEventArgs`
- `JobProgressChangedEventArgs`
- `JobActionChangedEventArgs`
- `JobImageGeneratedEventArgs`
- `JobCompletedEventArgs`

Each exposes `Guid JobId` and payload-specific fields; all are published through the shared `IEventService.Publish` pub/sub bus.

### Step 3 - Subscribe / Unsubscribe lifecycle

**Status:** [x] Delivered across Phases 5-8.

Every UI component that subscribes to events implements `IDisposable` and unsubscribes symmetrically:

| Component                   | Subscribes   | Unsubscribes in Dispose |
| --------------------------- | ------------ | ----------------------- |
| `SchedulerRunsTab.razor`    | 4 event args | yes                     |
| `SchedulerResultsTab.razor` | 4 event args | yes                     |
| `SchedulerEditorTab.razor`  | 1 event arg  | yes                     |

Event handlers all marshal UI updates through `InvokeAsync(StateHasChanged)` to stay on the render dispatcher.

### Step 4 - Theming and MudBlazor consistency

**Status:** [x] Audited.

All Scheduler UI surfaces use MudBlazor primitives already in use throughout the app:

- Layout: `MudContainer`, `MudGrid`, `MudItem`, `MudStack`, `MudPaper`, `MudDivider`
- Inputs: `MudTextField`, `MudNumericField`, `MudSelect`, `MudMenu`, `MudMenuItem`, `MudButton`, `MudIconButton`
- Feedback: `MudChip`, `MudProgressLinear`, `MudAlert`, `MudTooltip`, `MudTable`, `MudTabs`, `MudTabPanel`, `MudImage`
- Dialogs: `IDialogService.ShowAsync` + custom `SchedulerJsonEditorDialog`
- Snackbar: `ISnackbar.Add` for user feedback on Save / Duplicate / Delete / Schedule

Colors follow the app's severity convention: Success (Run), Info (Resume / Schedule), Warning (Pause), Error (Stop / Delete), Primary (Generic actions), Dark/Default (Cancelled / neutral status chips).

## Non-Scheduler Test Failures (Pre-Existing)

The full test run reports 18 failures unrelated to Scheduler code:

- 7x `WanLoaderFragmentTests` - `'clip_output' has not been registered` (workflow fragment harness)
- 3x `FragmentParametersExtensionsTests` - type coercion / culture-sensitive decimal parsing
- 3x `ParserTests` - culture-dependent `<lora:...:0,50>` vs `0.50` formatting
- 2x `WildcardServiceTests` - wildcard parsing / default collection seeding
- 2x workflow image-path tests (`ZImageImg2ImgWorkflowTests`, `QwenImg2ImgEditWorkflowTests`)
- 1x `SDTxt2ImgWorkflowTests.GetFragments_ShouldReturnExpectedCount` - expected 5 fragments, found 6
- 1x `StateServiceTests.LoadState_RestoresAssets`

None of these tests touch `BlazorWebApp.Scheduler.*`, `IJobRepository`, or the new events. They are tracked as pre-existing and out of scope for the Scheduler feature.

## Execution Checklist

| Step | Description                       | Status         |
| ---- | --------------------------------- | -------------- |
| 1    | `/scheduler` nav entry            | [x] Phase 5    |
| 2    | Job event args                    | [x] Phase 4    |
| 3    | Subscribe / unsubscribe lifecycle | [x] Phases 5-8 |
| 4    | Theming / MudBlazor consistency   | [x] Audited    |
