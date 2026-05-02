---
name: evented-feature-wiring
description: 'Wire cross-component and cross-service notifications through the repo pub/sub pattern. Use when publishing an event, subscribing to state changes, adding EventArgs types, replacing direct component coupling, or implementing decoupled communication through IEventService.'
user-invocable: false
---

# Evented Feature Wiring

Use this skill whenever a feature needs notifications outside a single component boundary.

## When To Use

- Publish a change or completion event
- Subscribe a component or service to domain changes
- Replace direct callbacks or ad hoc .NET events with the repo standard
- Add new event argument types under `Events/`

## Procedure

1. Define a typed `EventArgs` class under `BlazorWebApp/Events/`.
2. Name the event for the domain action or state change, not the UI control that triggered it.
3. Inject `IEventService` into the owning publisher.
4. Publish only after the relevant state mutation succeeds.
5. Subscribe consumers through `IEventService.Subscribe<TEvent>()`.
6. Unsubscribe when the consumer's lifetime ends so subscriptions do not leak.
7. Keep events payload-focused. Send the data consumers need instead of forcing them to reach back into UI state.
8. Add targeted tests when the event flow is critical to behavior.

## Guardrails

- Do not introduce raw cross-component references when the event service already fits.
- Do not invent parallel event buses.
- Do not skip unsubscribe paths for long-lived or repeat-mounted consumers.

## Key Anchors

- `../../copilot-instructions.md`
- `../../../BlazorWebApp/Services/IEventService.cs`
- `../../../BlazorWebApp/Services/EventService.cs`
- `../../../BlazorWebApp/Events/`
- `../../../BlazorWebApp/Components/Shared/WorkflowStrip.razor`