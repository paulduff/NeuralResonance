# Folded Archive Entry 109: Tartarus Cluster Deployment

## Decision

Dyad's first distributed body will use the Tartarus network as a measured set
of explicit neural-service owners. Distribution must not change circuit
semantics, insert host cognition, or silently omit a structure.

## Deployment Invariants

- Every Control Program registry structure is assigned to exactly one bundle.
- Every bilateral instance has a unique declared port on its host.
- WorldSim is the authoritative embodied world; MazeSim is optional diagnostic
  equipment.
- WPF clients stay on Windows. Structure services and the Control Program may
  run on Windows or Ubuntu.
- A non-loopback listener requires the shared transport secret.
- DNS, clock synchronization, free memory, free disk, executable presence, and
  port ownership are checked before a node starts.
- Failed preflight is evidence to fix the node, never permission to reduce the
  neural model automatically.

## Current Manifest

The manifest now covers 90 structures and 180 left/right service instances in
six biological worker groups, one required interactive/control group, and one
optional maze diagnostic. The example inventory uses stable `.tartarus` DNS
names and address offsets inside Paul's planned 20-address reservation without
inventing the actual subnet.

## Capacity Rule

Initial placement is deliberately coarse. Runtime telemetry will determine
whether a bundle should be divided further. CPU count alone is not sufficient:
working-set headroom, garbage-collection pauses, publish/snapshot latency, and
cross-node queue age must be measured together.

## Qualification Boundary

A valid manifest and passing node preflight prove deployment consistency, not
brain performance. Cluster qualification still requires a live run with all
structures healthy, bounded spike age, synchronized clocks, physical WorldSim
consequences, and no host-authored behavioural fallback.
