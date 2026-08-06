# Somatic Afferents Service Specification

Biological justification: primary somatic afferent neurons carry physical skin
deformation from low-threshold mechanoreceptors and high-threshold nociceptors.
The service receives receptor spikes only. It does not receive simulator labels,
values, actions, brain targets, or preclassified collision events.

- Receptor populations: Merkel/SA1 pressure, Meissner/RA1 onset, Pacinian/RA2
  vibration, Ruffini/SA2 stretch, and high-threshold mechanical afferents.
- Laterality: body-local contact geometry maps to the contralateral service;
  midline contacts are represented bilaterally.
- Feed-forward routes: somatothalamic perception and spinal reflex afference.
- Neuron model: Izhikevich.
- Plasticity: STDP.
- Conduction delay window: 1-6 ms.
