# Cortical memory fixture diagnosis

The completed cell-local timing suite reproduced the original
`NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation`
failure: initial score 0.000189, final 0.000035, reported strength 0.008270.
That assertion and the production learning parameters remain unchanged.

Source inspection identifies interpretation limits before diagnosing a cause:

- The fixture sends twelve events from the same source cell/contact at one
  timestamp. It is a synthetic input burst, not demonstrated cortical experience.
- Per repetition, timestamps advance by 55 ms but the two calls each declare
  only 10 ms of integration. Nine repetitions therefore declare 180 ms of
  integration over an outer timestamp span of 495 ms. Inputs can be processed
  late at the next covered interval. This is not continuous-time exposure.
- The displayed consolidation value is a derived score: normalized positive
  efficacy change multiplied by a function of `log2(UpdateCount+1)`, then averaged
  over supporting contacts. It is not a separate biological consolidation state.
  UpdateCount includes continuous modulation calls, not just neuronal experiences.
- Pair timing can cause potentiation or depression. Repetition alone does not
  establish a monotonic-potentiation requirement. Whether this particular failure
  reflects real efficacy loss, aggregation or fixture timing needs retained evidence.
- The original test generates fresh projection IDs, which participate in the
  sampled conduction delay. Thus repeated unit invocations need not exercise
  identical arrival timing. The diagnostic holds these identities fixed across
  controls; it is not an exact replay of the earlier failure's unrecorded IDs.

## Prepared capture

`MemoryConsolidationProbe` uses fresh, explicitly isolated state directories and
deterministic fixture identities. Its temporal-association profile matches the
original Izhikevich / STDP+SynapticTaggingCapture selection. It changes no rule,
gain, budget, trace constant, model fingerprint or membrane parameter.

Five cases compare:

1. Original simultaneous events and gapped integration, with observation.
2. The same case without observers, to check that observation does not alter state.
3. Simultaneous events with continuous 25/30 ms integration intervals.
4. Continuous integration with the twelve cue events spaced 1 ms apart.
5. Continuous simultaneous input without the fixture's modulatory events.

Spacing and modulator removal are diagnostic controls, not proposed physiological
defaults. All cases retain nine repetitions, full input/output events, tick signals,
acknowledgements and complete per-contact values. `MemoryLearningEvidence` adds
an opt-in observer around actual pre-, post- and modulation updates. It records
weight, tag, eligibility, pre/post traces, budget, activity and local modulation
before/after each operation. Service hosts do not install this observer.

The analysis requires complete case/input coverage, unchanged observer-on/off
contact and acknowledgement state, equivalent full outbound payloads apart from
fresh message IDs, reconstruction of the stored aggregate score and accounting
of each contact's signed changes. It reports efficacy separately from the score,
support counts, input lateness, elapsed intervals and pre/post/modulator changes.
Injected events are checked by message identity and source/transmitter coverage,
with recorded arrival/processing times joined to their presynaptic observations;
actual local cortical feedback is retained separately. The original
assertion's strongest-ensemble selection is reported alongside the cue contact's
ensemble, rather than assuming they are the same.
No increasing-score threshold is used to declare biological learning success.

## Execution and interpretation

`prepare-cortical-memory.py` freezes pushed source. The visible launcher first
runs the unchanged failing fixture and solution-manifest check. Only that exact
known assertion failure may continue into diagnosis; missing tests, another error
or a manifest failure stops the run. The original outcome is recorded separately.
The final status is DIAGNOSTICS_COMPLETE only when capture/analysis finish, and
the window prints whether the original assertion passed. It does not call the
full suite green or turn a known failure into a repaired learning mechanism.

The probe and test project builds passed with zero warnings/errors, and Python
AST/PowerShell parsing passed. These are build/static checks, not probe results.
No diagnostic outcome is claimed in this document. The operator reports completion;
there is no live monitoring, imposed duration ceiling or automatic restart. All
new state and evidence remain in the package with a verified D: backup. Existing
saves, running studies, Avatar, Unity and editor services remain untouched.

Next use the measured contact history to decide which fixture or mechanism needs
repair. Any biologically grounded consolidation replacement must have a justified
cell/contact model and causal controls; removing a failing assertion, increasing
a gain or making the display score monotonic would not establish that result.
