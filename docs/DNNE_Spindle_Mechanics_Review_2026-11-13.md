# Intrafusal mechanics source decision — 13 November 2026

Use the pinned 2026 author tutorial as the next **isolated mechanics reproduction
candidate**. It addresses two specific defects in the previously inspected 2020
implementation, bundles its dependencies, and retains a clear source license.
It is not ready for the live Avatar sensory path. No upstream model, test,
optimization or downloaded executable ran during this review.

The existing Avatar boundary map remains
`DNNE_Sensory_Boundary_Audit_2026-11-09.md`; the older source findings remain
`DNNE_Spindle_Source_Review_2026-11-09.md`. These findings apply to the specified
source revisions, not every implementation of the published model.

## Preserved evidence

`tools/capture-spindle-mechanics-review-20261113.py` checked all 1,066 extracted
files against four pinned ZIP archives and recorded 17 observations with source
hashes and line anchors. It uses only Python's standard library to inspect bytes
and text. It does not import or execute the upstream code. Its report is
`artifacts/spindle-mechanics-review-20261113/source-review.json`.

| Repository | Pinned revision | Files |
| --- | --- | ---: |
| Neuromechanics-Lab/Simha-Ting2023JExptPhysiol | `e82b0d05509283a3913fafec335d5451a4b7eb34` | 23 |
| Campbell-Muscle-Lab/MATMyoSim | `18bf2e30c6998e7bfa27169bc6ce1ca5551f7278` | 478 |
| SurabhiSimha/MATMyoSim | `62fa024e15ecd53b77da34a6346be8d706348090` | 502 |
| Neuromechanics-Lab/spindleModelTutorial | `721ba03cb775261548766ed1349c4bf2e0e35385` | 63 |

The first three are in the previously backed-up
`artifacts/spindle-mechanics-source-20261109`; their files were not changed.
The new tutorial is in `artifacts/spindle-2026-source-20261113`, with archive
SHA256 `050F71D846360410E4E917B74FB7A2F3E497C16E2107F0F53552C069D8869444`.
Initial extraction encountered Windows MAX_PATH. Extraction was completed using
extended paths, verifying previously extracted bytes unchanged. The complete
extracted tree subsequently matched the ZIP exactly.

Both new captures have byte-verified backups under
`D:\DNNE-desktop-runs\UnityWorld`: `spindle-2026-source-20261113` contains 65
files plus manifest, and `spindle-mechanics-review-20261113` contains the report
plus manifest. The previously verified 2023 source backup remains preserved.

The tutorial's retained root license is AGPL-3.0. Its `vendor/VENDOR_INFO.txt`
declares 17 bundled files and abbreviated dependency revisions `ba39645` and
`1107951`. The enclosing archive pins their actual bytes; no full dependency
commit is inferred from those abbreviations. No code was adopted into DNNE.

## Why move beyond the 2023 package

The 2023 model templates declare `2state`; the linked Campbell dependency's
kinetics dispatcher expects `2state_with_poly`. The author fork recognizes
`2state`, resolving that particular mismatch at source level. This is not proof
that the native model runs successfully.

Both supplied templates set actin deactivation `k_off` to zero. `simulateSarc.m`
loads and rewrites the input template, while protocol branches set different
subsets of parameters. A later protocol can therefore inherit an earlier
protocol's settings. Reproduction must use a fresh complete parameter snapshot
for each condition and preserve the original behavior as a separate control.
Do not silently relabel a patched execution as the published implementation.

## What improved in the 2026 mechanics

The [pinned author source](https://github.com/Neuromechanics-Lab/spindleModelTutorial/tree/721ba03cb775261548766ed1349c4bf2e0e35385)
uses the solver's evolving detached cross-bridge population and retains its final
state. Its slack objective evaluates passive stress at the trial length. Thus
the older cached-detachment and constant passive-root problems do not carry
over in those forms. Bag and chain actin deactivation are nonzero, with distinct
coefficients. These are implementation observations, not physiological or
numerical qualification.

The next reproduction still needs to expose these concrete concerns:

* **Two representations of cross-bridge state.** Evolution updates both `y` and
  `bin_pops`; length shifting changes `y` but does not synchronize `bin_pops` or
  `no_detached`. Force uses `y`, while the slack solver and reported populations
  read `bin_pops`. Compare both representations before and after every shift.
* **Slack flags and overlap boundaries.** Bag/chain checks are one `if/elseif`
  chain, leaving the other flag unchanged when either condition is taken.
  Overlap logic omits exact zero, while cooperative fluxes divide by overlap.
  Diagnose independent slack transitions and the defined length domain before
  correcting these paths; do not hide an undefined boundary with a clamp.
* **Time labels.** The driver advances for every timestamp, including the
  supplied initial `t=0`, then assigns those timestamps to the evolved outputs.
  Ten initial samples are forced to low activation, so their physical duration
  changes when timestep changes. A comparison must separate this protocol change
  from solver refinement and retain a genuine unevolved initial state.

## Why its output cannot be wired directly to Avatar

The conversion from fiber stress to receptor potential uses `filtfilt` across
the completed force trace. MATLAB documents forward and reverse filtering, so
this is offline processing with future-sample dependence. The code also passes
`cutoff/N/2` to `butter`: at its nominal 300 Hz setting this designs a 75 Hz
single-pass cutoff, rather than 300 Hz. The forward/reverse combination then
squares that response. This deduction follows the source expression and
[MATLAB's normalized-frequency convention](https://www.mathworks.com/help/signal/ref/butter.html)
and [filter definition](https://www.mathworks.com/help/signal/ref/filtfilt.html);
it is not a measured filter comparison.

Yank is now attached to completed intervals after the first sample, an
improvement over the older indexing. The first value still repeats a forward
difference. The converter drops NaN force samples and uses chain timestamps
without checking bag alignment. A live implementation must retain sample
identity, reject invalid or misaligned observations and avoid revising emitted
events when later observations arrive.

`integrateAndFire_v2.m` integrates a scaled, offset-removed signal to a fixed
threshold, resets it and counts refractory samples. Its timestep affects the
maximum rate and event quantization. The wrapper removes the first spike when
discarding its undefined instantaneous firing rate. That display convention
must never delete an actual afferent event. This remains a threshold/reset
generator, not the sought conductance-based sensory membrane.

The tutorial documentation explicitly describes fitting receptor potential,
self-generated example targets and supplied true burst timing. Such a fit does
not establish identified biological gamma drive or sensory membrane physiology.
Activation percentages/pCa and arbitrary receptor-potential units also do not
supply a calibrated neuronal current or gamma motor-neuron interface.

## Concrete next implementation boundary

1. Establish a native, reproducible mechanics reference before a live port.
   Freeze the vendored functions, full parameters, dependencies and function
   resolution. `setupTutorialPaths.m` otherwise prefers environment overrides or
   sibling checkouts. MATLAB/Octave was not found on PATH or in the two common
   installation directories checked; availability elsewhere is unestablished.
   Do not call a Python translation native-reference agreement.
2. Begin with imposed fascicle length and separate bag/chain drive fixtures.
   Record the original and any repaired candidates independently, including all
   state representations, stress, slack transitions, invariants, event-free
   initial state and endpoints. Include hold, stretch/shorten, slack/re-tension,
   history and independent static/dynamic activation. Prespecify tolerances
   after defining units and reference accuracy, before inspecting candidate
   results. Preserve failures; do not tune bounds to a trace.
3. Separate causal receptor transduction from the offline reference conversion.
   Prefix invariance is essential: supplying a later suffix must not change
   earlier output. Compare any justified causal filter/derivative candidate
   explicitly; do not silently correct the filter frequency in the reference.
4. Obtain and qualify an explicitly sourced afferent membrane and its mechanical
   current coupling. The bounded search has not located the Housley Ia deposit;
   this does not prove it is absent. No generic HH parameters, invented currents
   or threshold events should fill that physiological gap under a biological
   equivalence label.
5. Only then connect the reviewed physical-sample contract to Avatar's existing
   signed length/velocity path and actual afferent firings. Extrafusal force is
   not intrafusal spindle stress. Keep gamma motor drive, geometry and muscle
   reference length explicit. Live editor diagnostics must expose provenance,
   sample age, membrane events and missing calibration when that path changes.

This review changes no live brain, body, world, editor or save. The sole pending
experiment remains `golgi-sdirk3-fine-20261113-ca813f`; its outputs were not read.
CUDA host timing remains coded but unlaunched, gated on the operator's Golgi
completion report and review. No second experiment was started by this work.

## Subsequent preparation, not an executed reference

`tools/run_spindle_mechanics_reference.m` is an unfinished isolated harness for
the unchanged pinned 2026 bag/chain classes. It proposes 30 imposed-length
trajectories and two state-boundary records, preserving initial and completed
endpoint states, internal population representations, shapes and raw MAT output.
It does not run the full slack driver, receptor conversion or sensory membrane.
No Octave parse, model execution, refinement result or MATLAB agreement has been
established. A frozen preparer, independent PowerShell launcher and saved-output
analysis are still required before this harness can be used as evidence.

An isolated portable Octave 11.3.0 archive was downloaded from the
[official GNU Windows archive](https://ftp.gnu.org/gnu/octave/windows/octave-11.3.0-w64.zip).
The 855,208,207-byte archive has SHA-256
`b0e1bc72a87abe4e2a0cf7e76b7727d8fde5c99ec049c1576f095280c3e48e5a`.
Archive, download metadata and a full 59,532-file extraction manifest are retained
in `artifacts/octave-runtime-11.3.0-20261113`, with three files plus manifest
verified on D:. Extracted files are in `artifacts/portable-octave-11.3.0`.
No executable, post-install script, model or test has run. This is an HTTPS
download and recorded content hash; a publisher signature was not verified.
It does not alter the Python environment used by pending numerical studies.

Further bounded afferent-source discovery is retained in
`artifacts/spindle-afferent-discovery-20261113-fde0a8`, with 11 files plus manifest
verified on D:. Dryad metadata was accessible, but its model download endpoint
returned HTTP 401; no archive was retrieved. ModelDB requests timed out and the
bounded GitHub search did not locate the sought Housley source. These are search
limits, not evidence that a source does not exist. No unauthenticated alternative
or unreviewed model was adopted. Preparation paused to review the operator's
completed Unity/body-contact captures and repair their serialization failure.
