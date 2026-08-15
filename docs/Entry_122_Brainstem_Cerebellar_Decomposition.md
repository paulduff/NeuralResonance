# Entry 122 - Brainstem and Cerebellar Decomposition

## Decision

DNNE's next structural rung decomposes broad brainstem and cerebellar umbrellas
into thirteen explicit neuronal populations. Each population is an independently
hostable bilateral spiking service with local plasticity and routed synaptic
communication. None is an ML model, classifier, policy, or scripted behavioral
substitute.

## Added populations

| Population | Neuronal role |
| --- | --- |
| Red nucleus | Rubral integration of cerebellar correction and descending motor coordination |
| Pedunculopontine nucleus | Cholinergic locomotor-state, arousal, and REM coordination |
| Laterodorsal tegmental nucleus | Cholinergic REM, salience, thalamic, and forebrain state coordination |
| Parabrachial complex | Visceral, respiratory, nociceptive, and alarm-state relay |
| Principal sensory trigeminal nucleus | Craniofacial discriminative touch relay |
| Spinal trigeminal nucleus | Craniofacial pain, temperature, and defensive relay |
| Mesencephalic trigeminal nucleus | Jaw and craniofacial proprioceptive relay |
| Facial motor nucleus | Facial motor efference and corollary discharge |
| Oculomotor nucleus | Eye-orienting and vestibulo-ocular motor efference |
| Hypoglossal nucleus | Tongue and oral motor efference |
| Dentate nucleus | Lateral cerebellar planning output to thalamic and prefrontal circuits |
| Interposed nuclei | Limb-error correction through rubral and thalamic routes |
| Fastigial nucleus | Axial posture, balance, reticular, and autonomic correction |

## Neural implementation

- Every service uses Izhikevich spiking populations and local STDP.
- Brainstem and cranial populations preserve channel identity through a dedicated
  topographic kernel rather than flattening sensory and motor streams.
- PPN and LDT contain distinct wake-active and REM-active neuronal subpopulations.
- Named deep cerebellar nuclei receive Purkinje inhibition and mossy/climbing
  collateral context, then emit differentiated correction channels.
- Cholinergic cranial motor populations remain separate from the body locomotion
  decoder so facial, ocular, and oral activity cannot be mistaken for walking.
- Red, dentate, interposed, and fastigial populations contribute neuronal evidence
  to body motor correction and postural support.

## Connectome boundaries

The connectome now includes reciprocal and inhibitory paths among motor cortex,
thalamus, reticular formation, inferior olive, named cerebellar nuclei, rubral
output, vestibular nuclei, trigeminal relays, hypothalamus, insula, amygdala, and
cranial motor nuclei. Broad pons, medulla, deep cerebellar, and spinal motor
services remain temporary compatibility boundaries for older persisted synapses
and peripheral effectors.

## Result and limits

The atlas contains 125 protocol structures, corresponding to 250 bilateral
runtime instances and 238 rendered anatomical instances after midline
cardinality is respected. Atlas positions are representative audit geometry, not
voxel-level human segmentation. Peripheral facial, ocular, and oral musculature
is still exposed through the shared embodiment boundary; later work can split
those effectors without changing the neuronal source populations established here.
