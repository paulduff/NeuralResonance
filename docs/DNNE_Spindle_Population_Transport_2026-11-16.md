# Conservative spindle population transport candidate

The completed force review in `DNNE_Receptor_And_Slack_Results_2026-11-16.md`
finds sign-changing force discontinuities in all 31 saved states that lack an
unloaded root. The next investigation concerns distribution transport before
selecting a slack equilibrium or coupling the spindle to Avatar.

The existing `ControlProgram/PhysicalBodyTransducerRuntime.cs` muscle sensory
mapping and `src/NRE.SimAvatar/AvatarDynamicMuscle.cs` boundary were inspected.
The former still generates population stimuli from normalized measurements;
the latter is an isolated extrafusal muscle candidate. Neither is replaced by
this intrafusal numerical component. No live source or Unity caller selects it.

## Source and changed interpretation

The pinned `Neuromechanics-Lab/spindleModelTutorial` revision is
`721ba03cb775261548766ed1349c4bf2e0e35385`. Its Bag/Chain classes describe
`y(2:...)` as populations in displacement bins, multiply attachment rates by
bin width, and sum bin populations to obtain total attached myosin. However,
`shift_cbDist` samples linear interpolation at shifted bin centres and assigns
zero outside the endpoint centres. An occupied endpoint can disappear abruptly
when a query crosses that endpoint. Lost population is added to detached state.
The retained upstream source and AGPL-3.0 license remain authoritative baseline
evidence; the source's boundary convention is not a physiological measurement.

`tools/spindle_population_transport.py` interprets a stored bin population as
uniform mass over a cell of the declared width. Its faces lie half a bin outside
the bin centres. On the original 81-bin, 0.5 nm grid this gives cell faces at
-20.25 and +20.25 nm. That is an explicit changed numerical interpretation,
not a claim to preserve the old interpolation exactly or identify anatomical
detachment limits.

A shift deposits each source cell's mass into its two overlapping destination
cells. Mass crossing either external face is returned as separate left/right
outflow. No state is normalized, clipped, reset or silently dropped. A future
caller must account for this outflow explicitly; calling it biological
detachment requires justification and domain-extension checks. The component
does not update neuronal, actin-activation or cycling-kinetic state.

The separate reference constructs intersections of translated source intervals
with destination intervals. It calculates both the rebinned cell-centre force
and the exact first moment of the retained piecewise-constant distribution.
Those differ at partly occupied boundaries. Agreement between two remappers
therefore cannot conceal the cell-centre force approximation. Source force
coefficient 69,000 and power stroke 2.5 nm are preserved for these comparisons.

## Frozen comparison rig

The component and comparison rig are authored but **unexecuted and unqualified**.
The prospective protocol declares ten analytic controls, nine translations of
each of the 2,812 saved states (25,308 queries), and separate
range/resolution/history checks.
The arithmetic limits retain 1e-10 population/conservation, 1e-7 Pa force and
1e-8 state-domain bounds. These cover agreement for the same discretization;
they do not define physiological accuracy or allow the old force bounds to be
relaxed. Negative roundoff in the saved source remains visible.

The independent rig now requires:

- Known empty/interior/edge/full-outflow translations and explicit outflow
  ledgers, including the old endpoint-discontinuity counterexample.
- Every saved-state query and both sides of all reported no-root sign jumps.
- Analytic translating distributions at widths 0.5, 0.25, 0.125 and 0.0625 nm;
  compare force moments as well as population error.
- Extended numerical ranges and forward/reverse histories that expose
  accumulated projection diffusion and boundary loss. Smaller time steps alone
  can increase repeated-remapping diffusion and are not sufficient evidence.
- Frozen source/input/dependency provenance, D: backups and a separate visible
  PowerShell window. No controls, model calculations or new comparisons run
  during authoring/preparation.

`analyse-spindle-population-transport.py` preserves all candidate/reference
population arrays for the 25,308 queries, plus their force/outflow/error records.
It probes each saved sign-changing discontinuity from all 31 no-root states at
the boundary and on both sides. The continuity bound follows from the remap's
population and force-weight bounds; it does not presume a new zero-force root
exists. No roots are searched or selected by this experiment.

The 144 history cases vary two numerical half-ranges (20.25 and 40.25 nm),
four widths, three initial rectangular distributions, three subdivisions per
movement leg and forward-only/forward-return paths. A separate analytic
characteristic solution tracks which parts of the initial distribution survive
the entire absorbing-boundary history. Every endpoint retains population,
outflow, first/second-moment and force errors. No kinetic equations are advanced.

All histories must preserve population accounting and nonnegativity within the
declared arithmetic bounds. The 36 spatial comparisons additionally require the
finest grid's maximum population L1 error over the history to be at most 0.8 of
the coarsest grid's error, or 1e-10 if larger. This is a prospective engineering
convergence requirement, not a fit to the completed study or a physiological
accuracy threshold. Intermediate resolutions and all force/second-moment errors
remain reported. Temporal refinement and finite-domain physiological adequacy
are not declared passing merely because this arithmetic gate passes.

The domain checks here concern represented myosin mass/outflow. This rig does
not update actin activation or filament overlap after moving a population and
cannot qualify a complete intrafusal state transition. Boundary loss remains
explicit numerical outflow, with its physical interpretation still unqualified.

`prepare-spindle-population-transport.py` requires clean pushed source, the
operator-reported and D:-verified force review, matching native/source records
and unchanged numerical dependencies. It parses saved observations but executes
no transport or force comparison. `start-spindle-transport-window.ps1` owns all
controls and numerical stages in the independent visible window, postchecks
the protected package/dependencies, and verifies the final D: backup.

Only after these checks should a candidate replace distribution translation in
an isolated coupled-kinetics comparison. Root continuation, imposed geometry,
slack history, physiological calibration, sensory firing and Avatar recovery
remain separate requirements. The receptor crossing diagnostic `c099e3` remains
unmonitored and requires its own operator completion report. No native C#,
Avatar, Unity, receptor equations or preserved old force rule changed here.
