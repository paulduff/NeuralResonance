# Entry 060 - Dyad Survival Replay Evaluation

Date: 2026-07-19

## Purpose

This entry implements Upgrade 2 from Entry 058. Dyad can now replay a completed deterministic survival artifact into a private DNNE state and ask Entity for a bounded language candidate at selected historical checkpoints.

The replay cannot alter the recorded survival actions, DNNE reward, DNNE memory, or the live control program state. It is an audit instrument: the episode happened first; Entity is asked afterwards to offer language for DNNE review.

## Evidence Per Replay Turn

Each replay turn records:

- the original policy action and DNNE tick;
- the complete bounded prompt and prompt fingerprint;
- Entity version, configuration, candidate text, and source references;
- DNNE's independently recreated grounding snapshot;
- the review decision and reason, or the DNNE-only fallback reason.

The evaluator rebuilds the private DNNE state from the artifact's initial network-state document, then replays the original object, environment, body, motor, and outcome feedback at every recorded step. It verifies that the final replay tick matches the artifact before returning the result.

## Control Cases

The evaluator deliberately preserves three modes:

- **Entity available:** the candidate is validated by `dyad.language-candidate.v1` and passes through DNNE's existing review gate.
- **Entity unavailable:** DNNE narration is recorded as `dnne-fallback`; no candidate review is created.
- **Entity malformed:** a candidate failing the contract is replaced by the same `dnne-fallback`; no candidate review is created.

Neither a valid review nor `AcceptedForReview` emits a motor action, updates reward, writes memory, or changes the survival policy. They remain language evidence only.

## Running It

First create a benchmark artifact using Entry 059. Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\replay-survival-benchmark-dyad.ps1" `
  -ArtifactPath "C:\path\to\survival-benchmark-YYYYMMDD-HHMMSS.json"
```

With the Entity bridge disabled or unavailable, this produces a valid DNNE-fallback control artifact. To request actual Entity candidates, configure `NRE_ENTITY_ENABLED=true`, `NRE_ENTITY_CHECKPOINT_PATH`, and the normal Entity bridge settings before starting the DNNE Control Program.

Results are written under `tools\artifacts\dyad-survival-replay` by default. The JSON is the primary evidence; the Markdown report is a compact comparison aid.

## Boundary Kept

This work strengthens the language boundary rather than widening it. It is only after reproducible comparisons show useful, grounded improvement that Dyad should consider richer verified-memory excerpts or additional language evaluation tasks.
