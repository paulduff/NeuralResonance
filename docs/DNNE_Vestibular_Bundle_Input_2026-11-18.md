# Physical vestibular input boundary

The current skull adapter measures head motion, and the clocked labyrinth
preserves its physical sample clock. The next connection still needs a real
dimensional conversion: the inherited canal coordinate called `Cupula` is in
rad/s, while the sensory membrane requires bundle displacement in nanometres.
Otolith output is in m/s². Neither quantity can be passed directly as nanometres.

The existing live `VestibularLabyrinthTransducer` instead converts these reduced
coordinates into normalized activation. `PhysicalBodyTransducerRuntime` then
generates population messages. Those messages are not observations of an active
sensory axon's membrane. The isolated observed ingress now passes its component
checks, but this does not calibrate the physical stimulus or qualify afferent
firing. Existing live services remain unchanged.

## Declared conversion and ownership

`AvatarVestibularBundleInput` owns the unchanged clocked mechanics and requires
an explicit `AvatarVestibularBundleProjection`. There are no default conversion
coefficients or physiological presets. Each projection declares its hair-cell
identity, parameter provenance, receptor and signed dimensional coefficients:

- Canal: `x_nm = offset_nm + gain_nm_per_rad_per_second * cupula_rad_per_second`.
- Otolith: `x_nm = offset_nm + dot(gain_nm_per_m_per_second_squared, acceleration_m_per_second_squared)`.

These are linear mechanical hypotheses. The canal choice is one of the six
existing directions; the otolith vector uses skull right/down/forward axes.
Unused coefficients must be zero, preventing accidental mixing of units in a
single receptor. Finite zero gain is permitted as an explicit disconnected
diagnostic, not an implicit default. The projection and parameter source enter
the owner's fingerprint. A source label or hash does not prove calibration.

All coefficients in the new tests are arbitrary, clearly marked dimensional
controls. No physical gain has been inferred from a desired firing rate. Actual
cupula-to-bundle deformation, otolith compliance and receptor orientation still
need defensible parameters and independent mechanical qualification. Nonlinear
deformation and local hair-bundle adaptation are not supplied by this adapter.

Each sample retains the original kinematics, mechanical receipt, session/sample
identity, source observation hash and displacement. The initial observation
releases no elapsed interval. Each subsequent endpoint releases exactly the
preceding physical interval, using the preceding projected displacement.
The newly projected endpoint cannot affect the past.

The mechanics already holds earlier head measurements between physical samples;
this adapter adds a second, explicit hold of the projected mechanical endpoint
between samples. This is a sampling approximation requiring refinement, not an
exact reconstruction of continuously moving cupula or hair bundles. It introduces
no fitted biological conduction delay. A closed-loop scheduler must accommodate
actual sample availability without inventing silent sensory or motor intervals.

The skull adapter cannot derive acceleration from its first endpoint. Its
`FirstMechanicsStep` therefore remains explicit; no earlier input history is
filled in. A proposed sensory step must fit wholly inside a released interval.
Crossing an input boundary requires a compatible sensory grid or a separately
qualified solver change; this adapter does not shorten steps silently.

Only one released interval can await acknowledgement. A new body endpoint is
rejected before mechanics advances if that interval remains pending. Exact last
replay is inert, including after acknowledgement; changed, stale, missing and
foreign samples reject. There is one thread owner. An overflow after an accepted
mechanical transition retains the accepted mechanics and attempted input, faults
the adapter, and emits no invented bundle sample. No clipping, gain adjustment,
reset, probability-to-spike conversion or active membrane equation is added.

## Component check

The prospective window contains 317 cases: all 264 reviewed sensory/receiving
checks, 16 clocked mechanical checks, 12 skull checks and 25 new bundle checks.
New controls include explicit dimensions and provenance, directional mapping,
causal holds, first acceleration availability, bounded input delivery, exact
retries, rejection before mutation, overflow preservation and receipt round trips.
One wiring control passes a released physical interval through the actual event
observer/outbox using the earlier severed passive cable fixture. Its crossing is
not physiological sensory firing. A separate control compares every mechanical
receipt with the unchanged original mechanics.

`vestibular_bundle_gate.py`, `start-vestibular-bundle-window.ps1` and
`review-vestibular-bundle.py` preserve source/runtime evidence from the completed
ingress capture. Source is frozen at a clean pushed revision, backed up to D:
before launch, and run in visible PowerShell without agent monitoring. The
completed package receives the usual verified backup. Tests have not executed
during authoring. The final full authoring build, including the wiring controls,
passed with zero warnings and errors. Static inventory finds exactly 317 cases;
all 56 protected source files match the completed ingress source. Python AST,
PowerShell parsing and whitespace checks pass. Build evidence is retained at
`D:/DNNE-build-work/vestibular-bundle-authoring-20261118/build-final.log`.

No Unity/editor live selection changes. The next coupled experiment must bind
the explicit physical-input owner to the fresh cell/session, retain original
input receipts, qualify physical and sensory sampling, and check active firing.
The two older windows, sensory precision `ddef1a` and motor comparison `e9bed6`,
remain pending and untouched. Physiological rest, mechanical calibration,
continuous sensory firing, closed-loop balance and fall recovery remain open.


The physical bundle component window is now launched as
`vestibular-bundle-20261118-092238`, frozen at pushed revision
`2debce1486ded90f94d3f585f8fa07b2a71b4f94`. Frozen static preflight passed;
1,959 initial files plus manifest were D: verified before visible task
`DNNE-Vestibular-Bundle-471197ff7f26` started. Launch returned successfully.
No post-launch output, status or process was inspected. All 317 declared cases
execute only in that independent PowerShell window; no success is inferred.
Exactly THREE pending reports: bundle `092238`, sensory precision `ddef1a` and
saved motor comparison `e9bed6`. Ingress `d519cf` is reviewed/passing, not pending.
No physiological calibration or live brain/body selection changed. Full goal
remains active and unfinished; retain all original evidence and saves.


## Completed component review

The operator reported `092238` complete. Review verifies 2,503 files per C:/D:
copy, 1,950 frozen Git files and all 317 focused cases, with exact saved-analysis
reproduction. The one-file review artifact is D: verified. This qualifies the
component controls for dimensional mapping and causal physical intervals; it
does not calibrate mechanical gains or qualify active afferent firing/balance.
Sensory precision `ddef1a` is also reviewed; only older motor `e9bed6` remains
pending before the next candidate launch.
