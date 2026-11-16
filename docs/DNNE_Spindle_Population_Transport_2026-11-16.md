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

## Qualification still to be implemented and run

The component is authored but **unexecuted and unqualified**. Its prospective
protocol declares ten analytic controls, nine translations of each of the
2,812 saved states (25,308 queries), and separate range/resolution/history checks.
The arithmetic limits retain 1e-10 population/conservation, 1e-7 Pa force and
1e-8 state-domain bounds. These cover agreement for the same discretization;
they do not define physiological accuracy or allow the old force bounds to be
relaxed. Negative roundoff in the saved source remains visible.

Before launching, complete the independent rig with:

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

Only after these checks should a candidate replace distribution translation in
an isolated coupled-kinetics comparison. Root continuation, imposed geometry,
slack history, physiological calibration, sensory firing and Avatar recovery
remain separate requirements. The completed receptor failure was reported while
this candidate was being authored; its numerical diagnosis takes priority over
launching an unfinished transport rig.
