# Folded Archive Entry 103: Delete Stale Semantic Observers

## Decision

The maze, world simulator, and editor may display measured neuronal decoder
state, but they must not recover cognition from retired host-authored semantic
state. Read-only tools are still part of the scientific boundary: what they
choose to present can otherwise make an obsolete symbolic subsystem appear to
remain authoritative.

## Changes

- The world simulator no longer polls the deleted object-memory endpoint.
- Named planning, goal, intent, target, and motor-directive readers were
  removed from the world simulator.
- Maze cognition panels now show numeric neuronal perception, memory, and
  affect populations.
- Editor dashboard and telemetry formatters now use the current neuronal
  decoder states only.
- Retired scalar limbic, global-neuromodulator, narration, and grounded-label
  readers were deleted.
- A regression test pins the desktop observer boundary.
- The repository verifier now skips a retired test-project path instead of
  failing after all valid projects and tests have passed.

## Runtime Invariant

Desktop applications may report numeric neuronal measurements and measured
body or environment state. They cannot synthesize, restore, or imply a
semantic decision that the running neuronal circuits did not produce.

## Result

This rung reduces polling load and removes misleading compatibility output. It
does not increase capability or claim that a decoded measurement is itself a
thought; it makes the remaining evidence easier to interpret honestly.
