# Entry 123 - Cortical Laminar Microcircuits

## Decision

The 35 atlas-backed cortical areas now contain explicit neuronal layer and
interneuron populations. The change refines each existing cortical service
without creating replacement classifiers, host policies, or machine-learning
models. Learning remains spike-driven local plasticity.

Neuron identities remain `n-###` for readable live telemetry. A neuron's numeric
index now assigns it to one of eight populations:

| Population | Share | Principal role | Output transmitter |
| --- | ---: | --- | --- |
| L1 modulatory | 5% | Distal apical and neuromodulatory integration | Glutamate |
| L2/3 intratelencephalic | 30% | Recurrent and corticocortical integration | Glutamate |
| L4 input | 15% | Thalamic and peripheral sensory input | Glutamate |
| L5 pyramidal tract | 15% | Descending cortical output | Glutamate |
| L6 corticothalamic | 15% | Corticothalamic feedback | Glutamate |
| PV interneuron | 10% | Fast perisomatic inhibition | GABA |
| SST interneuron | 5% | Dendritic inhibition | GABA |
| VIP interneuron | 5% | SST inhibition and local disinhibition | GABA |

The aggregate is 80% excitatory and 20% inhibitory. A 384-neuron cortical
service therefore contains 308 excitatory and 76 inhibitory model neurons.

## Functional microcircuit

Afferent spikes retain their established topographic, semantic, body-map, or
attention channel before being placed into a laminar target:

- thalamic and peripheral sensory drive enters L4;
- corticocortical feedforward drive enters L2/3;
- feedback enters L1 or L6;
- diffuse cholinergic, monoaminergic, and septal input enters L1.

Every firing cortical neuron also creates one bounded, delayed local collateral.
The collateral passes through the normal receptor and STDP path on the next tick:

- L4 excites L2/3;
- L2/3 recurs locally or recruits L5;
- L5 recruits L6;
- L6 feeds L4;
- PV inhibits L2/3 or L5;
- SST inhibits L1/apical integration;
- VIP inhibits SST, allowing disinhibition to emerge neurally.

Only L2/3, L5, and L6 pyramidal populations emit the existing long-range
projection. Local inhibitory neurons no longer produce anatomically incorrect
long-range GABA output through a cortical area's default tract.

## Observation

`CorticalLaminarDiagnostics` travels from each structure service through
ControlProgram into the browser editor. Selecting a cortical area exposes live
neuron counts, active counts, firing rates, and inhibitory balance for all eight
populations. This makes a silent or saturated layer visible during embodied
tests without adding a non-neuronal control channel.

## Compatibility and cost

- Structure count remains 125 and bilateral service count remains 250.
- Neuron counts and live neuron IDs are unchanged.
- Existing long-range connectome routes remain separate from cortical tissue.
- Local collaterals stay inside the structure process, avoiding network traffic.
- One bounded local spike is scheduled per cortical action potential; queue
  capacity remains authoritative under overload.
- Synapse persistence is generation 2. Older unversioned stores are intentionally
  ignored so pre-laminar weights cannot be attached to a neuron with a new cellular
  meaning. The previous local generation was removed before the next live run, so
  the network begins with fresh synapses.

## Verification

Focused tests cover the complete cortical set, stable IDs, the 80/20 population
distribution, population transmitters, laminar afferent routing, local plastic
collaterals, long-range pyramidal output, live diagnostics, and all existing
purpose-specific cortical kernels.
