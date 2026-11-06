# Cell-local arrival integration candidate

The [completed boundary study](DNNE_Boundary_Timing_Results_2026-11-06.md)
identifies a numerical route by which an arrival changes a cell that never
receives it. This candidate keeps the chronological delivery loop and gives
each isolated Purkinje cell its own last-integrated time. A cell advances to
its own incoming event, to the regular physical grid, or to the host endpoint.
An arrival for another cell and a no-op marker do not divide its dynamics.

An actual arrival still releases transmitter after the receiving cell has
advanced to that time. Receptors, dendrites, membrane, support variables and
contact chemistry use the cell's complete elapsed interval. Feed-forward and
feedback events retain their target mapping, identities, conduction delays and
times. Late transport remains reported at processing time, without rewinding.

## Scope

The constructor option is internal and restricted to fresh isolated Purkinje
populations. The public service constructor cannot enable it. Candidate saves
have a distinct `isolated-cell-local-arrivals-v1` fingerprint; diagnostic labels
report that version. Production defaults and their fingerprints are unchanged.
No existing saves are loaded or upgraded, and no live brain or body is started.

This is not yet the general recurrent-network scheduler. Cortical local routing,
multi-target arbors, simultaneous recurrent events and concurrent transport
ingress need separate causal qualification before any service adoption. Global
queue inspection is acceptable for this isolated rig, not presented as a cluster
scaling solution. Off-grid host endpoints can still partition dynamics; this
candidate does not claim arbitrary packet-cadence invariance or numerical accuracy.

Biological parameters, imposed Purkinje drives, channel equations, calcium LTD
and learning magnitudes are unchanged. The separate Golgi membrane/cable work
remains next cell-development work; repairing this demonstrated scheduling issue
takes precedence before interpreting more connected comparisons.

## Independent PowerShell verification

The launcher freezes committed source, rebuilds it in a new capture and runs:

1. The full DNNE unit suite, including 15 timing cases covering both original and
   candidate modes, real arrival causality, fractional feed-forward/feedback
   times, late delivery, aligned packet duration, no-op invariance, unrelated-cell
   invariance, target responsiveness, service exclusion and distinct fingerprints.
   The previously documented cortical-consolidation failure is explicitly
   recorded if it recurs; only that exact known failure may permit continuation.
   Any new failure or missing timing cases stops the experiment.
2. Eight seconds for each of 48 conditions: both global and cell-local integration,
   two fixed input offsets, four receiving ceilings, and connected/disconnected/
   disconnected-with-markers controls. Every original-mode condition must match
   the corresponding earlier capture's full recorded cell-state, voltage/energy
   and contact CSV hashes and spike train. This extends the original 16 baseline
   checks to all 24 original-mode cases, including markers.
3. The original nine damaged-capture checks, plus candidate corruptions affecting
   an untouched cell's voltage, its contact trajectory and the declared mode.
   A failed locality preflight stops before the long stage.
4. The same 48-condition matrix for 60 biological seconds each.

Candidate acceptance requires exact sampled state and spike agreement in the
untaught cell across all three input conditions, and in both cells between the
two disconnected conditions. Complete recorded contact trajectories must agree
for the same comparisons. All declared real inputs, fractional arrivals, no-op
counts, contact ownership, finite state, energy and calcium accounting remain
required. The target's biological response is retained; the tests explicitly
reject silently discarding actual inputs as a route to invariance.

The source snapshot, binaries, inputs, validators and prior expected hashes are
retained. The window hashes protected older files before and after execution,
backs the new capture up to D:, and stays visible. There is no live monitoring;
the operator reports completion before results are inspected.

## Current status

Development Release builds of the probe and DNNE test project succeeded without
warnings or errors. The first frozen run's 1,075 unit cases gave 1,074 passes and
one solution-manifest failure. All 15 timing cases passed; the runner had
incorrectly expected 16 timing cases / 1,076 total. The separate cortical-
consolidation case passed this time, which does not establish that its earlier
failure is repaired. No timing comparison ran.

The unit assembly was outside the frozen `source` subtree. Source-reading tests
therefore climbed into the working repository and saw ongoing development as well
as the committed Golgi-channel project missing from the solution. The repair adds
the committed project, puts build output beneath frozen `source/artifacts`, and
checks the discovered root before tests. All source-reading tests now resolve the
snapshot. The count is corrected to the actual declared 15 timing cases, without
removing any case or weakening its assertions. The replacement package excludes
unfinished Golgi-cell development through an explicit committed-source archive.

The operator closed the failed window during its protection audit. Its initial
RUNNING status was stale; the saved TRX and terminal task establish the failure.
The failed capture is preserved separately, and the launcher now records failures
before starting lengthy cleanup. The replacement comparison is pending the
operator's report; no timing-study pass or physiological qualification is claimed.
Whole-cell accuracy, meaningful repeated teaching and Avatar recovery require
their separate evidence. Neither an all-green locality result nor the existing
Golgi channel result would establish those outcomes.
