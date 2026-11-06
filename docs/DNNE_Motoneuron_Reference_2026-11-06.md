# Isolated conductance-based spinal motor-cell candidate

The existing SpinalCordMotor host selects generic Izhikevich cells. The completed
motor-unit calcium checks establish a transport and force component, not the
electrical validity of its neuronal source. This candidate supplies a specific
motor-cell membrane for that missing qualification. No live ModelNeuron, host,
Avatar, Unity world or editor selects it.

## Reference and source conventions

The reference is [Powers and Heckman (2017)](https://doi.org/10.1152/jn.00850.2016),
[*Synaptic control of the shape of the motoneuron pool input-output function*](https://pmc.ncbi.nlm.nih.gov/articles/PMC5340877/),
with deposited code at [ModelDB 239582](https://modeldb.science/239582),
[revision 0d11354490fad5fcd940d016a8d69b000693fd81](https://github.com/ModelDBRepository/239582/tree/0d11354490fad5fcd940d016a8d69b000693fd81).
The first declared parameter set is `FivecomptcMG_0.hoc`, a low-threshold cat
medial-gastrocnemius reference. It is not a calibrated human Avatar motor pool.
The other deposited pool members have not been imported into the runtime.

`MotoneuronChannelKinetics` independently expresses the deposited Na transient
and persistent currents' gates, delayed potassium, somatic calcium/KCa,
dendritic L-type calcium and HCN gates. `MotoneuronReferenceCell` couples one
capacitive soma to four dendritic compartments. These are reduced electrical
compartments; their long equivalent cylinders must not be rendered as literal
anatomical soma dimensions in the editor.

The two massless soma endpoints each join two dendrites. Their exact algebraic
elimination retains the small cross-dendrite conductance as well as soma-to-
dendrite coupling. It is not four independently approximated soma resistors.
Areas use the cylindrical lateral membrane and one segment per section, matching
the deposit's default discretization. A further morphology-refinement claim
would require its own study.

Several otherwise implicit source choices are explicit:

- Analytic rate equations replace the source's 1 mV lookup tables. Sodium's
  removable singularities use their continuous limits/series, not a tiny constant
  band. This is not bit-identical native NEURON reproduction.
- Sodium reversal is +50 mV. The somatic calcium pool in `mAHP.mod` is local,
  not a `USEION ca WRITE cai` state. Its calcium reversal therefore remains the
  simulator parameter `12.5*log(2/5e-5)` mV; it is not recomputed from local calcium.
  This follows the [NEURON ion-style rules](https://www.neuronsimulator.org/en/latest/progref/modelspec/programmatic/ions.html)
  and [8.2.4 defaults](https://www.neuronsimulator.org/en/8.2.4/doxygen/membdef_8h_source.html).
  Dendritic L-type calcium uses its separate fixed +80 mV reversal.
- Only the deposited fast sodium rates use their temperature scaling. Unused
  temperature variables in other mechanisms do not imply extra scaling.
- The local somatic calcium pool retains the source's inward-only influx and
  linear removal toward 0.0001 mM. The influx rectification is a declared model
  reduction, not a numerical repair. Modern Faraday 96485.33212 C/mol is explicit.
- Calcium removal and fixed reversal reservoirs provide an unlimited reference
  bath. ATP, oxygen/perfusion and ionic-reservoir homeostasis are not modelled.

## Dynamics and scope

There are 27 physical variables: five voltages, nine soma gates, local somatic
calcium, and three gates for each dendrite. Three further integrals account for
calcium entry/removal and net external membrane charge. Coupled RK4 advances all
variables together with a maximum declared step of 0.0125 ms. Non-finite or
out-of-domain states fault the candidate; there is no voltage reset, gate clamp,
prescribed spike train, refractory timer or imposed periodic drive.

Upward soma crossings of -20 mV are observations only, with linear crossing-time
interpolation. They are not verified axonal propagation. Diagnostic inputs are
constant somatic current in pA and excitatory/inhibitory conductance in nS per
dendritic compartment for each interval. Callers must split at input changes.
No stochastic rate generator, live synapse/receptor lane, learning rule,
neuromodulatory transport, neuronal motor publisher or checkpoint is connected.

## First verification stage

The operator-reported capture `motoneuron-reference-20261106-f949c7` passed all
10 tests, 96 clamps and nine passive traces. Its final backup initially failed
at a 260-character build-file path. The backup is now repaired and fully verified;
the original failure status is preserved. See
`DNNE_Motoneuron_Passive_Results_2026-11-06.md` for evidence and scope.

Builds of the runtime, focused test assembly and probe pass with zero warnings
or errors. The rig contains nine focused test cases plus the solution-manifest
case. It checks gate domains, singularities, calcium dependence, passive branch
coupling, equilibrium, current superposition, charge accounting, copy isolation
and invalid-input rejection. These are not free-firing or recovery tests.

`MotoneuronReferenceProbe` captures 96 voltage/calcium clamps: two temperatures,
eight voltages, three calcium levels and two slow-sodium availability settings.
The availability-zero setting is a declared diagnostic perturbation; the source
cell has availability one. Twelve gates are recorded at seven times.

The probe also records nine passive cable traces: three currents at three RK4
steps, each lasting 100 ms with 0.25 ms output. All active membrane conductances
are disabled explicitly for these cable checks. The full active candidate is
implemented but is not selected in this first experiment.

`motoneuron-reference-check.py` reads numeric declarations from the pinned MOD
files and HOC overrides. It independently computes exact clamped relaxation.
For the cable it constructs the seven-node conductance matrix, eliminates the
two massless nodes by dense linear algebra, and compares with a continuous
matrix-exponential reference. It does not use the candidate's numerical trace
as its reference or execute the original HOC/NMODL programs.

Predeclared criteria are 1e-12 absolute gate/equilibrium error, 1e-10 ms rate-time
constant error, 1e-7 mV passive voltage error and 1e-5 fC charge-ledger residual.
The coupling-matrix comparison allows 1e-9 nS. Source, binaries, input data and
results are frozen and backed up. The visible PowerShell launcher has no imposed
duration ceiling and is not monitored live; the operator reports completion.

Passing this stage permits active numerical comparison, not live adoption. Next
require independent active voltage/calcium and spike-time refinement, rest,
recruitment, afterhyperpolarization, persistent excitation and inhibitory recovery
under matched inputs. Only then join real cell events to the separately checked
motor-unit consumer and qualify the sensory/spinal/body loop. Preserve Avatar's
existing receptors and mechanics, and update Unity/editor/save fingerprints when
the live pathway changes. Old saves remain untouched throughout this isolated work.
