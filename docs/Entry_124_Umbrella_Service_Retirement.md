# Entry 124 - Umbrella Service Retirement

## Decision

Regional umbrella names are no longer runnable DNNE structures. They remain only
as atlas grouping language. Every service, route endpoint, registry entry, and
telemetry identity now denotes a concrete neural area, nucleus, tract, or
peripheral neural interface.

## Retired identities

- `Thalamus`
- `Amygdala`
- `Hypothalamus`
- `GlobusPallidus`
- `DeepCerebellarNuclei`
- `Medulla`

`Pons` was renamed to `PontineNuclei`, and `BasalForebrain` was renamed to
`NucleusBasalis`, because those services already implemented the narrower
populations.

## Result

- 119 concrete protocol and registry structures
- 238 bilateral runtime service instances
- 228 atlas-rendered instances after midline cardinality is applied
- 450 projection routes with every structure represented as a source and target
- one canonical shared `StructureCircuitProfile`

Compatibility-weighted diagnostics were removed. Amygdala summaries average the
five explicit amygdala/extended-limbic populations, cerebellar summaries include
all three deep nuclei, REM/wake summaries use PPN and LDT, and autonomic summaries
use NTS and parabrachial activity. The generic reticular formation no longer
doubles as a medulla signal.

## State policy

No old service state is migrated. The project is still in structural development,
so a corrected fresh neuronal network is preferable to preserving unaccepted
weights with ambiguous identities. Future compatibility constraints begin only
after a learned network is explicitly accepted for preservation.

## Guardrail

`UmbrellaRetirementTests` checks the protocol, registry, distributed manifest,
connectome, project tree, and Blazor atlas as one invariant. Reintroducing a
retired service or omitting a concrete structure must fail validation.
