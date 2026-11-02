# Matched olivary response and recovery experiment

The completed olive calibration restored the baseline voltage waveform after
challenges, but did not deliver a fresh strong input afterwards. It therefore
did not establish recovered responsiveness. This experiment tests that missing
behavior using the same production cell and reference channel parameters.

No membrane, receptor, learning, energy, Avatar or editor implementation changes
are included. The freeze script compares every shared runtime, protocol and
contract source file with the completed calibration package. The existing
1,034-test production regression remains applicable to that identical source.
This is an isolated diagnostic rig, not a new live stimulation or restart policy.

## Matched design

There are **160 conditions**, each a new deterministic cell for 60 simulated
seconds: five interventions, two probe schedules, eight onset offsets and two
outer steps (1 and 0.125 ms). The reference channel settings remain CaT 1.1,
CaH 4.5, HCN 0.12, calcium-dependent K 35 and axonal K 240. Fresh initialization
is explicit; all initial settling is recorded.

| Intervention, seconds 10–30 | Supply / inhibitory input |
|---|---|
| Sham | Healthy reference bath; no added GABA |
| Inhibition, one contact | One GABA contact at 100 Hz; healthy bath |
| Inhibition, four contacts | Four distinct GABA contacts at 100 Hz; healthy bath |
| Glucose withdrawal | Glucose availability 0; oxygen and perfusion 1 |
| Oxygen withdrawal | Oxygen availability 0; glucose and perfusion 1 |

At 30 s, normal supply returns and challenge GABA stops. These are reduced
availability variables and provisional contact densities, not a claim to match
human tissue exposure. The stronger inhibitory condition tests convergence from
several contacts without changing the cell's receptor parameters.

Each excitatory probe activates **four distinct glutamate contacts**, each at
quanta 1 with the existing 1 ms release kinetics. Inputs are sparse, spaced
997 ms apart within each epoch. There are eight baseline probes, twenty during
the challenge, and thirty after restoration. The second schedule omits the twenty
challenge probes, leaving the same baseline and post-restoration schedule. This
separates recovery after a quiet challenge from behavior with continuing probes.
Sham controls use the corresponding identical schedule.

Offsets are 0, 15, 30, 45, 60, 75, 90 and 105 ms. They sample different initial
oscillator states; they are not independent biological subjects. Subsequent
stimuli may change oscillator timing. Do not interpret pooled deterministic
trials as biological replication or attach population confidence intervals.

The exact schedule, for offset `o`, is:

- Before: `2000 + o + i*997` ms, `i=0..7`.
- During: `10000 + o + i*997` ms, `i=0..19`, omitted in the quiet-challenge schedule.
- After: `30000 + o + i*997` ms, `i=0..29`.

The thirty after-probes are labelled early/middle/late in blocks of ten, rather
than by rounded clock boundaries. Each response window is 250 ms, completes
before the run ends, and stays within its intervention epoch. Windows never
overlap. The final probes occur at approximately 58.9–59.0 s. This avoids the
unequal endpoint counts found in the previous weak paired-input experiment.
This run tests recovery, not an optimum inhibitory-to-excitatory delay.

## Measurements and checks

The rig saves every input (including simultaneous excitation and inhibition),
every somatic and axonal crossing, pre-probe voltage/calcium/reserve, and 2 ms
voltage/gate/energy/supply traces. For each probe, it records:

- Somatic and axonal spike counts in `(onset, onset+250 ms]`.
- First somatic and axonal response latencies, or null when absent.
- Spikes in the preceding 250 ms, to expose background activity.

All spikes outside response windows are counted separately. A window-associated
spike is not automatically caused by the probe; delayed or background events
must remain visible. The analysis reports hit fractions, mean output per probe,
axonal burst size when responsive, and latency for each phase. It computes
differences against the matched sham at the same offset, schedule and outer step.
It does not silently drop failures or cap burst counts.

Baseline traces must match exactly across interventions and schedules for each
offset. Input schedules are independently reconstructed by the Python analyser.
It checks all complete windows, actual release counts, simultaneous input delivery,
event attribution, finite states, supply transitions and energy conservation.
Even a short preflight generates and validates all full-run schedules. Numerical
completion is separate from recovered responsiveness or physiological qualification.

The final review should establish whether baseline inputs evoke output, whether
GABA suppresses that output at the tested strengths, how fuel withdrawal affects
it, and whether late post-restoration responses return toward their matched sham.
Compare response probability, somatic events and axonal burst size together.
Check for persistent off-window activity and whether probing during the challenge
changes the result. A recovered resting waveform alone is insufficient.

## Validation, execution and preservation

`PosturalCellProbe --olive-response --smoke --quick` exercises all five interventions
and both schedules at both steps for the first offset. Its four-second preflight
has one before-probe, an optional during-probe and one after-probe. This validates
plumbing and accounting, not prolonged fuel effects. The independent visible
PowerShell performs the full 160-condition short preflight before the 60 s study.

Validator tests reject a missing simultaneous inhibitory input, an altered
response-window endpoint, an incorrect latency, corrupted energy or supply,
an unmatched baseline and a missing full-run probe. Production source is unchanged;
the probe Release build and short functional comparisons are checked separately.
The local short comparison completed all 20 cases and validated 40,000 trace rows;
all ten coarse/fine trace comparisons matched. All seven corruption checks rejected
the altered evidence. A direct comparison confirmed all 58 shared runtime,
protocol and contract files match the completed calibration package.

`tools/prepare-olive-response.py` requires clean committed/pushed source, verifies
the final executable hashes and the successful short comparison, and freezes the
code, binary, analyser, preparation evidence and prior findings. It inherits all
older protection hashes and adds the complete olive calibration and its review at
both C: and D:. A unique D: backup is verified before launch, and the launcher
verifies preservation and backs up the new run at completion/failure. Old saves
are never loaded, overwritten or upgraded.

The run uses an independent visible PowerShell window with native console logs,
FINISHED/FAILED status and no live agent monitoring. No live brain, world or Avatar
is started. Connected olivary/Purkinje teaching and physical recovery remain
separate acceptance work after the saved response results have been reviewed.
