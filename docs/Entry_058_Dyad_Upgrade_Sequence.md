# Entry 058 - Dyad Upgrade Sequence

Date: 2026-07-19

## Purpose

Record the next upgrades for Dyad: Entity as the bounded language component and DNNE as the grounded embodied system. The sequence is designed so meaningful progress continues on current CPU hardware, while the incoming RTX workstation accelerates later model training and inference without determining the architecture.

## Guiding Rule

DNNE must be measurable before Dyad is allowed to influence more than reviewed language candidates. Entity may improve expression, interpretation, and teaching, but it is not evidence of world state and it does not replace DNNE's perception, memory, reward, action selection, or safety constraints.

## Upgrade 1 - Deterministic Survival Benchmark Harness

Implement Phase 1 of Entry 055 as the immediate next engineering milestone.

- Run a small headless world with fixed seeds, terrain, resources, darkness, shelter, and threat conditions.
- Start each episode from a known brain snapshot and configuration.
- End on a fixed tick budget, success condition, or terminal failure.
- Record world seed, configuration, input traces, body state, actions, outcomes, terminal reason, and wall-clock cost.
- Emit a machine-readable episode artifact and a compact Markdown report.

This work is CPU-friendly and establishes the evidence base needed before claims about learning, adaptation, or Dyad benefit.

## Upgrade 2 - Dyad Replay and Evaluation

Use recorded DNNE snapshots and episode traces to exercise the existing Dyad language boundary.

- Submit a bounded set of verified-state prompts to Entity.
- Record the Entity version, generation settings, full bounded prompt, candidate text, source references, DNNE grounding snapshot, and review decision.
- Compare Entity enabled, Entity unavailable, malformed Entity output, and DNNE-only fallback runs.
- Verify that the adapter never creates motor output, reward updates, memory writes, or survival-policy changes.

The target is not to make Entity more influential. The target is to make every interaction traceable and falsifiable.

## Upgrade 3 - Provenance-Led Knowledge Curation

Continue improving Entity's training and retrieval material without treating volume as quality.

- Curate historical and scientific material with source, licence, date, perspective, known limitations, and stable references.
- Keep multiple perspectives where accounts conflict, especially for history shaped by conquest, colonialism, or institutional power.
- Deduplicate, normalise, split by source, and retain held-out evaluation sets.
- Add tests that reward cited uncertainty and penalise unsupported confident claims.

Entity should learn to distinguish sourced statement, interpretation, uncertainty, and hypothesis.

## Upgrade 4 - Cluster and Resilience Preparation

Prepare the Tartarus network before assigning distributed workloads.

- Document host names, reserved addresses, roles, operating systems, storage, and GPU capability.
- Establish DNS, time synchronisation, SSH or equivalent administration, shared-secret handling, monitoring, and service health checks.
- Segment the cluster with a management or service VLAN where practical.
- Design staged shutdown: solar or battery alert, network notification, checkpoint flush, service stop, then UPS final protection.
- Keep early distributed work limited to corpus processing, evaluation, batch jobs, and service orchestration rather than attempting synchronous cross-machine neural simulation.

## RTX Workstation Role

The RTX workstation should initially be used for:

- faster Entity inference and chat evaluation;
- tokenizer, context-window, and model-size experiments;
- GPU-backed training experiments after a reproducible CPU baseline exists;
- checkpoint conversion, validation, and benchmark comparison.

It is an accelerator, not a substitute for the benchmark, audit, or grounding work above.

## Order of Work

1. Build the deterministic survival benchmark harness.
2. Produce replayable episode artifacts and baseline comparisons.
3. Exercise Dyad against recorded states with DNNE-only fallback controls.
4. Expand provenance-led data and evaluation material.
5. Bring the RTX workstation online and compare CPU versus GPU training and inference.
6. Prepare the cluster for resilient batch and service workloads.

## Success Condition

Dyad becomes more capable only when a recorded comparison shows improvement without weakening DNNE's grounded authority, reproducibility, or safety boundaries.
