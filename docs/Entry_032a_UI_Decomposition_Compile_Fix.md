# Entry 032a — UI decomposition compile fix

This hotfix corrects the first pass of Entry 032 after local compilation surfaced two issues:

1. `Home.razor` did not import the extracted tab component namespace (`NRE.Blazor.Shared.OperatorConsole.Tabs`). Because the components were unresolved, Razor interpreted their attributes as plain HTML attributes, which produced the `CS1660` lambda-to-bool errors.
2. `SystemsTabPanel.razor` used inline lambdas with nested quoted string literals inside `@onclick`, which led the Razor source generator to emit malformed code (`CS1026`).

## Applied corrections

- Added `@using NRE.Blazor.Shared.OperatorConsole.Tabs` to `src/NRE.Blazor/Pages/Home.razor`.
- Replaced the three inline sleep-transition lambdas in `src/NRE.Blazor/Shared/OperatorConsole/Tabs/SystemsTabPanel.razor` with named methods:
  - `OnForceSleepAwake()`
  - `OnForceSleepNrem()`
  - `OnForceSleepRem()`
- Removed the unused helper function `R` from `src/NRE.Core/Engine/AnatomyValidationHarness.cs`.

## Expected effect

The extracted tab components should now resolve properly from `Home.razor`, and the systems tab should no longer generate Razor parser errors during build.
