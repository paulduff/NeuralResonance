# Distributed Neural Network Engine (DNNE) Architecture Overview

## Connectivity Diagram
```text
Feed-forward hierarchy (primary direction):
Sensory Inputs -> Thalamus -> Primary Cortices -> Association Cortices -> PFC/PPC -> SMA/M1
                                   \-> Hippocampal Loop (EC -> DG -> CA3 -> CA1 -> Sub -> EC)
                                   \-> Basal Ganglia Loop (Striatum -> GP/STN -> SNr -> Thalamus)
                                   \-> Limbic Loop (Amygdala <-> PFC/ACC)
                                   \-> Cerebellar Loop (Granule -> Purkinje -> DCN -> M1/Thalamus)

Explicit feedback pathways (is_feedback=true):
PFC/PPC  --(5-20ms)--> Thalamus
CA3      --(2-5ms)-->  CA3 (recurrent)
DCN      --(10-15ms)-> M1
SNr      --(8-12ms)--> Thalamus --> PFC
CeA      --(5-10ms)--> PFC
CA1/Sub  --(3-8ms)-->  EC
LC/Raphe/BF/VTA --(0ms via NeuromodState)--> All services
```
## Messaging and Isolation Choice
All structure services use **HTTP/2 + gRPC-style protobuf envelopes** over isolated ASP.NET Core hosts to preserve low payload overhead and deterministic millisecond-scale delivery; conduction delays are represented explicitly in `is_feedback` queues rather than implied by transport latency, matching biological axonal delays while preserving strict no-shared-memory service isolation.

### Primary Visual Cortex (V1)
This structure is implemented as an isolated service with profile-configured neurons because Orientation columns, ocular dominance stripes, simple/complex cells with feed-forward LGN convergence and lateral inhibitory interneurons; BCM plasticity stabilizes orientation selectivity. Inputs arrive from LGN thalamocortical GLUT; outbound spikes project to PPC GLUT, TemporalAssociation GLUT, Thalamus GLUT (feedback) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Primary Auditory Cortex (A1)
This structure is implemented as an isolated service with profile-configured neurons because Tonotopic lamina with onset and sustained populations to preserve temporal envelope and spectral place coding under inhibitory surround sharpening. Inputs arrive from MGN GLUT; outbound spikes project to TemporalAssociation GLUT, PPC GLUT, Thalamus GLUT (feedback) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Primary Somatosensory Cortex (S1)
This structure is implemented as an isolated service with profile-configured neurons because Somatotopic barrels merge RA and SA streams via parallel fast/slow receptor channels and columnar inhibitory gating for tactile contrast. Inputs arrive from VPL/VPM GLUT; outbound spikes project to PPC GLUT, M1 GLUT, Thalamus GLUT (feedback) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Olfactory Bulb
This structure is implemented as an isolated service with profile-configured neurons because Mitral/tufted relay with periglomerular and granule-cell inhibition produces gamma-band packetization of odor identity and intensity. Inputs arrive from Peripheral receptor drive (simulated afferent); outbound spikes project to TemporalAssociation GLUT, AmygdalaLA GLUT, EntorhinalCortex GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Thalamus (LGN/MGN/VPL/VPM)
This structure is implemented as an isolated service with profile-configured neurons because Relay and matrix cells switch tonic/burst by membrane bias; matrix projections broadcast context while relay nuclei preserve modality fidelity. Inputs arrive from Sensory relay afferents, TRN GABA, SNr GABA; outbound spikes project to V1 GLUT, A1 GLUT, S1 GLUT, PFC GLUT, PPC GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Thalamic Reticular Nucleus (TRN)
This structure is implemented as an isolated service with profile-configured neurons because Pure GABAergic shell gates thalamic throughput with winner-take-most inhibition implementing selective attention. Inputs arrive from Thalamus collateral GLUT, PFC/PPC GLUT; outbound spikes project to Thalamus GABA with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Entorhinal Cortex (EC)
This structure is implemented as an isolated service with profile-configured neurons because Grid-like phase-coded cells in layer II provide sparse indexing while layer III carries cortical context for comparator circuits. Inputs arrive from TemporalAssociation GLUT, OlfactoryBulb GLUT, CA1/Subiculum GLUT (feedback); outbound spikes project to DentateGyrus GLUT (LII), CA1 GLUT (LIII), CA3 GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Dentate Gyrus (DG)
This structure is implemented as an isolated service with profile-configured neurons because Massive expansion recoding and sparse winner-take-all pattern separation with mossy-fiber LTP and adult-neurogenesis placeholder pools. Inputs arrive from EntorhinalCortex GLUT; outbound spikes project to CA3 GLUT (mossy fibers) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### CA3
This structure is implemented as an isolated service with profile-configured neurons because Autoassociative attractor network with recurrent Schaffer-like collaterals supports pattern completion and sequence retrieval. Inputs arrive from DentateGyrus GLUT, EntorhinalCortex GLUT, CA3 recurrent GLUT (feedback); outbound spikes project to CA1 GLUT, CA3 recurrent GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### CA1
This structure is implemented as an isolated service with profile-configured neurons because Comparator microcircuit contrasts Schaffer collateral recall with direct EC drive and encodes mismatch for episodic prediction error. Inputs arrive from CA3 GLUT, EntorhinalCortex GLUT; outbound spikes project to Subiculum GLUT, EntorhinalCortex GLUT (feedback), PFC GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Subiculum
This structure is implemented as an isolated service with profile-configured neurons because Principal hippocampal output with place-modulated bursting relays contextual memory to cortical and motivational circuits. Inputs arrive from CA1 GLUT; outbound spikes project to EntorhinalCortex GLUT (feedback), PFC GLUT, NucleusAccumbens via Striatum GLUT proxy with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Prefrontal Cortex (PFC)
This structure is implemented as an isolated service with profile-configured neurons because Recurrent microcolumns maintain working memory with D1 stabilization and D2 flexibility gates under dopamine-modulated STDP/tagging-capture. Inputs arrive from Thalamus GLUT, PPC GLUT, TemporalAssociation GLUT, CA1/Subiculum GLUT, CeA modulation, SNc/VTA dopamine via NeuromodState; outbound spikes project to PPC GLUT, SMA GLUT, Striatum GLUT, Thalamus GLUT (feedback), ACC GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Posterior Parietal Cortex (PPC)
This structure is implemented as an isolated service with profile-configured neurons because Multimodal map integrates spatial salience and sensorimotor transforms using competitive attentional fields. Inputs arrive from V1/A1/S1 GLUT, Thalamus GLUT, PFC GLUT; outbound spikes project to PFC GLUT, SMA GLUT, Thalamus GLUT (feedback), ACC GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Temporal Association Cortex
This structure is implemented as an isolated service with profile-configured neurons because Ventral-stream style object and semantic assemblies bind multimodal identity codes with associative recall. Inputs arrive from V1 GLUT, A1 GLUT, OlfactoryBulb GLUT; outbound spikes project to PFC GLUT, EntorhinalCortex GLUT, AmygdalaLA GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Striatum (D1/D2 MSN)
This structure is implemented as an isolated service with profile-configured neurons because Direct/indirect pathway competition selects actions through dopamine-modulated STDP on corticostriatal synapses. Inputs arrive from PFC/PPC/SMA GLUT, Thalamus GLUT, SNc/VTA dopamine; outbound spikes project to GlobusPallidusExternal GABA, GlobusPallidusInternal GABA, SNr GABA with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Globus Pallidus (GPe/GPi)
This structure is implemented as an isolated service with profile-configured neurons because Tonic inhibition forms disinhibitory control where pauses release thalamocortical channels for selected motor/cognitive plans. Inputs arrive from Striatum GABA, STN GLUT; outbound spikes project to SNr GABA, Thalamus GABA (GPi), STN GABA (GPe) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Subthalamic Nucleus (STN)
This structure is implemented as an isolated service with profile-configured neurons because Fast global NoGo bursts transiently raise basal ganglia inhibitory output to stop or reset action programs. Inputs arrive from PFC/SMA hyperdirect GLUT, GPe GABA; outbound spikes project to GlobusPallidus GLUT, SNr GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Substantia Nigra pars reticulata (SNr)
This structure is implemented as an isolated service with profile-configured neurons because Principal basal ganglia output gate; tonic inhibitory firing sculpts thalamic relay availability. Inputs arrive from Striatum GABA, STN GLUT, GPi GABA; outbound spikes project to Thalamus GABA (feedback loop) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Substantia Nigra pars compacta (SNc)
This structure is implemented as an isolated service with profile-configured neurons because Computes reward prediction error and updates tonic/phasic dopamine for corticostriatal and prefrontal plasticity control. Inputs arrive from Striatum/brainstem reward cues; outbound spikes project to Global NeuromodState dopamine with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Amygdala (LA->B->ITC->CeA)
This structure is implemented as an isolated service with profile-configured neurons because Fear-conditioning microcircuit computes salience with ITC inhibitory gates controlling CeA output. Inputs arrive from TemporalAssociation GLUT, OlfactoryBulb GLUT, Thalamus coarse GLUT; outbound spikes project to PFC GABA/peptidergic proxy via CeA modulation, ACC GLUT, hypothalamic proxy with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Anterior Cingulate Cortex (ACC)
This structure is implemented as an isolated service with profile-configured neurons because Conflict and error monitoring population drives adaptive control and effort allocation. Inputs arrive from PFC/PPC GLUT, Amygdala GLUT, Thalamus GLUT; outbound spikes project to PFC GLUT, LC/BasalForebrain modulatory drive proxy with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Granule Cell Layer (Cerebellum)
This structure is implemented as an isolated service with profile-configured neurons because Expansion recoding of mossy fibers creates sparse high-dimensional context for precise timing learning. Inputs arrive from Mossy fiber proxies from PPC/SMA/vestibular; outbound spikes project to PurkinjeCellLayer GLUT (parallel fibers) with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Purkinje Cell Layer
This structure is implemented as an isolated service with profile-configured neurons because Purkinje cells integrate parallel fiber context with climbing-fiber teaching spikes and express cerebellar LTD. Inputs arrive from Granule parallel fibers GLUT, InferiorOlive COMPLEX; outbound spikes project to DeepCerebellarNuclei GABA with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Deep Cerebellar Nuclei (DCN)
This structure is implemented as an isolated service with profile-configured neurons because Disinhibited rebound firing emits corrected motor commands and timing predictions. Inputs arrive from PurkinjeCellLayer GABA, mossy/climbing collaterals GLUT; outbound spikes project to M1 GLUT (feedback), Thalamus GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Inferior Olive
This structure is implemented as an isolated service with profile-configured neurons because Electrotonically coupled oscillators produce synchronized complex spikes encoding movement error. Inputs arrive from Motor error signals from ACC/M1 proxy; outbound spikes project to PurkinjeCellLayer COMPLEX glutamatergic climbing fibers with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Locus Coeruleus (LC)
This structure is implemented as an isolated service with profile-configured neurons because Global noradrenergic gain modulation toggles exploration-vs-exploitation and sensory signal-to-noise. Inputs arrive from ACC/arousal cues; outbound spikes project to NeuromodState norepinephrine broadcast with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Raphe Nuclei
This structure is implemented as an isolated service with profile-configured neurons because Serotonergic tone modulates patience, temporal discounting, and plasticity thresholds. Inputs arrive from Hypothalamic/brainstem cues; outbound spikes project to NeuromodState serotonin broadcast with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Basal Forebrain Cholinergic
This structure is implemented as an isolated service with profile-configured neurons because Cholinergic transients sharpen cortical receptive fields and gate attention-dependent learning. Inputs arrive from ACC/PFC attentional demand; outbound spikes project to NeuromodState acetylcholine broadcast with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Ventral Tegmental Area (VTA)
This structure is implemented as an isolated service with profile-configured neurons because Novelty/reward dopaminergic bursts reinforce learning and working-memory updating. Inputs arrive from Hippocampal novelty and reward cues; outbound spikes project to NeuromodState dopamine broadcast with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Primary Motor Cortex (M1)
This structure is implemented as an isolated service with profile-configured neurons because Layer V corticospinal populations generate motor output with beta-band coordination and sensorimotor integration. Inputs arrive from S1/PPC/SMA GLUT, Thalamus GLUT, DCN feedback GLUT; outbound spikes project to Spinal output proxy GLUT, InferiorOlive error proxy, Brainstem motor proxy with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.

### Supplementary Motor Area (SMA)
This structure is implemented as an isolated service with profile-configured neurons because Pre-motor sequencing network prepares and chunks action plans before execution. Inputs arrive from PFC/PPC GLUT, Thalamus GLUT; outbound spikes project to M1 GLUT, STN GLUT (hyperdirect), Striatum GLUT with neurotransmitter identity as the synaptic signal; inbound and outbound pathways are constrained to SpikeMessage contracts, and feedback participation is enabled when listed in explicit feedback pathways with delayed reverse-queue processing to avoid synchronous loops while preserving biologically realistic recurrence.


