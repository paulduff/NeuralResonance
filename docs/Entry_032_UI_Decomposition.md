# Entry 032 — UI Decomposition Pass

## Purpose
Reduce operator-console fragility by extracting stable sidebar UI sections from `Pages/Home.razor` into focused Razor components without changing the runtime semantics of the console.

## Motivation
The stabilized canon removed API/UI contract drift, but the main Blazor operator page remained a large monolith that mixed:
- status summary rendering
- toolbar actions
- tab selection
- stimulus controls
- neuromodulator controls
- pons controls
- scenario orchestration
- systems monitoring controls
- transport and polling logic
- JS interop and frame handling

That layout made the page expensive to evolve and raised regression risk whenever a new notch touched nearby UI markup.

## Applied decomposition
The following components were introduced under `src/NRE.Blazor/Shared/OperatorConsole/`:

- `StatusSummaryPanel.razor`
- `ControlToolbar.razor`
- `SidebarTabBar.razor`
- `ConsoleTabs.cs`
- `Tabs/StimulusTabPanel.razor`
- `Tabs/ModulatorsTabPanel.razor`
- `Tabs/PonsTabPanel.razor`
- `Tabs/ScenesTabPanel.razor`
- `Tabs/SystemsTabPanel.razor`

## What stayed in Home.razor
`Home.razor` still owns:
- HTTP orchestration
- save/load actions
- polling loops
- fast-frame parsing
- JS interop calls
- monitor, peer, voice, and view tabs pending later extraction

This keeps the pass incremental and lowers the probability of introducing behavioural regressions.

## Structural benefit
This pass creates a cleaner separation between:
- **presentation components** for sidebar controls
- **page orchestration** for data fetching, renderer updates, and action dispatch

It also centralises sidebar tab definitions in `ConsoleTabs.cs`, reducing magic-string drift.

## Intent for next notch
The next UI-focused decomposition should target:
1. Monitor tab extraction
2. View tab extraction
3. Voice and peer tab extraction
4. optional service extraction for polling and renderer transport

## Expected outcome
The console should behave the same, but the codebase should be easier to maintain and safer to extend as further biology and rendering notches are applied.
