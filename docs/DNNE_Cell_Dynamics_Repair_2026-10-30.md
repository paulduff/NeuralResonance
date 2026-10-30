# Cell response and timing repair — 30 October 2026

The completed cell experiment identifies defects below the balance decoder.
The old cerebellar HH implementation ignored the tested inputs in its spike output;
other cells changed materially when only the integration step changed. The repair
addresses those mechanisms. Cerebellar physiology, balance decoding and physical
recovery remain unqualified.

## Completed evidence

`artifacts/postural-cells-20261019-d6e2e8` finished on 19 October: 72 full conditions,
eight simulated seconds each, 32 fresh cells per condition. Reviewed after Paul's
completion message on 30 October. All 386 captured files match the D: copy and
manifest; 1,676 protected files and 232 packaged files retain their hashes. Review:
`artifacts/cell-repair-20261030/completed-study-review.json`. Original captures and
saves remain unchanged.

Inputs have identical physical event times and a held 1 ms circuit clock:

| Cell/input | Mean stimulus firing, 1 ms step | At 0.25 ms |
| --- | ---: | ---: |
| Vestibular LIF, 100 Hz glutamate | 7.641 Hz | 6.750 Hz |
| Vestibular LIF, three times the quanta | 42.438 Hz | 27.805 Hz |
| Fastigial Izhikevich, 100 Hz glutamate | 18.000 Hz | 19.992 Hz |
| Fastigial Izhikevich, three times the quanta | 56.086 Hz | 47.984 Hz |
| Vermis HH, pacemaker on, any of six inputs | 34.500 Hz | 35.250 Hz |

At each tested step, all 32 pacemaker-driven HH cells preserve their exact
stimulus spike times between no input, glutamate, stronger glutamate, GABA and
mixed input. Without the circuit pacemaker they emit no spikes in any condition.
The post-step AIS voltage is at a boundary for about 83–84% of the pacemaker-on
baseline capture. Current reaches the cells without altering their spike output.
The pacemaker switch has no effect in the other two profiles because their
circuit pacemaker implementation supplies zero. These are model observations,
not biological measurements; no contacts, routing, whole brain or body was tested.

## Applied corrections

Continuous terms now use elapsed time: dendritic forcing, ongoing plateau/calcium
accumulation, activity-related potassium load and glial transmitter clearance.
Spike impulses retain fixed contributions per event. Clearance composes over
elapsed milliseconds instead of applying the same fraction once per call.

The intracellular support approximation no longer decays history on input arrival
in addition to normal time advance. Relaxation uses exponential elapsed-time
updates without a one-millisecond floor; its spike contribution is an event
impulse. This repairs its software clock, not its biological validation.

`HodgkinHuxleyMembrane` integrates voltage and persistent sodium activation,
sodium inactivation and potassium activation gates. Its classical reference uses
the modern -65 mV convention, capacitance 1 microfarad/cm², conductances
120/36/0.3 millisiemens/cm², reversal potentials 50/-77/-54.387 mV and gate
kinetics at **6.3°C**. Sources:
[Hodgkin & Huxley, 1952](https://doi.org/10.1113/jphysiol.1952.sp004764) and
[NEURON's reference mechanism](https://github.com/neuronsimulator/nrn/blob/master/src/nrnoc/hh.mod).
This is a reference membrane, **not a calibrated mammalian Purkinje, vermis or
inferior-olive cell**. Temperature and cell-specific channels require calibration.

Coupled RK4 uses at most 0.025 ms internal steps, reduced for stiff conductances
or rapid voltage movement. Nonfinite/excessively stiff state is rejected rather
than silently reset. HH spikes are upward zero crossings; the membrane and gates
continue through the spike and recovery. The old AIS trigger, imposed reset and
generic reset-model adaptation increment no longer generate HH spikes. Outer HH
event intervals must be at most 1 ms; StructureEngine already substeps host ticks.

The old 2,000-unit pacemaker conversion becomes an explicitly **provisional**
10 microamp/cm² per normalized drive unit. Receptor amplitudes become conductances
matched at -65 mV, with assumed excitatory/GABA-A/GABA-B reversals 0/-80/-95 mV.
Driving forces are evaluated inside the membrane solver, so inhibition does not
act as a fixed outward current at every voltage. These conversions, receptor
kinetics, metabolic and dendritic approximations are not fitted human measurements.
Other cell families retain their current-based receptor interpretation.

Dynamics version `physical-time-hh-gates-v2` is included in checkpoint model
fingerprints. Earlier schema-4 weights are rejected without overwrite; they are
not silently reinterpreted. Contact identity is unchanged. The existing editor
learning inspector receives the new version and HH reference/calibration status.

## Validation and next comparison

All **1,007 regression tests pass**, including six new cell checks and preservation
of a save with an old dynamics fingerprint. Coverage includes rest, gate-rate
singularities, voltage-clamp history, time composition, unreset action potentials,
agreement with a separate 0.001 ms numerical reference, input-sensitive cerebellar
firing, glial timing and intracellular-support cadence. Avatar, learning, routing,
motor authority and editor contracts also pass. Four existing diagnostic projects
missing from the solution manifest are now listed; its check passes too.

The diagnostic Release build has zero warnings/errors. All 144 short conditions
pass input/time accounting and matching fresh baselines. Python syntax and
PowerShell parsing pass. Accepted evidence is `checks/regression-accepted.trx` under
`artifacts/cell-repair-20261030`; earlier failed development checks remain separate.

The next independent PowerShell window runs preflight, repeats the frozen old
72-condition executable on the currently installed runtime, then tests the new
model at **1, 0.5, 0.25 and 0.125 ms** (144 conditions). This avoids attributing a
runtime upgrade to the repair. Paired response and convergence reports are saved,
protected hashes checked, and results backed up to D:. No live agent monitoring.
Process completion is not physiological acceptance.

Avatar's original balance and neuronal motor bridge were revisited. Mechanics,
labyrinth/receptors, neuronal authority and paused-start behaviour are retained.
No live services are redeployed by this diagnostic. Population-error cancellation
is still a separate decoder problem. Corrected cell responses, complete cerebellar
teaching inputs and a calibrated neuronal disturbance representation must precede
another physical recovery claim.
