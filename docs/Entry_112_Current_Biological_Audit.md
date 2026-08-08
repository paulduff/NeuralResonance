# Folded Archive Entry 112: Current Biological Audit

Date: 2026-08-08

## Decision

Use the refreshed DNNE biological comparison audit as the current evidence
baseline for architecture, qualification, and future claims.

## Measured State

- 90 named `StructureId` values.
- 90 registered structures and 180 bilateral service instances.
- 90 structures passing the circuit mapping audit.
- 14 shared circuit-family kernels.
- Region-scaled populations of 160 to 512 simulated neurons per structure
  instance.

The circuit audit now parses the final enum member even when it has no trailing
comma and fails when the protocol enum and service registry disagree. A
regression test pins the same invariant in the normal test suite.

## Claim Boundary

DNNE is a distributed spiking simulation inspired by named neural systems. It
is not an anatomical or physiological replica of a human brain, and current
evidence does not support claims of human-equivalent cognition, consciousness,
or personhood.

The existing system is substantial enough to test neuronal causality,
embodiment, learning, timing, and cross-structure integration. Its most
important remaining scientific gaps are scale, cell-type diversity,
atlas-derived connectivity, parameter calibration, biophysical transduction,
developmental and glial mechanisms, distributed timing measurement, and
controlled behavioral validation.

## Next Standard

Future biological upgrades should arrive with citations, seeded qualification
scenarios, ablation controls, baseline comparisons, and repeatable evidence.
Service health and visible movement are integration evidence; they are not by
themselves evidence of biological fidelity or cognition.

The full baseline and reproduction commands are recorded in
`docs/dnne/BIOLOGICAL_COMPARISON_AUDIT_2026-03-26.md`.
