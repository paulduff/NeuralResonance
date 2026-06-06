# PPC Service Specification

Biological justification: Posterior parietal cortex areas 5/7 integrate S1 body-zone input, vestibular posture, dorsal visual motion, and retrosplenial spatial reference into a body schema and peripersonal attention map.

Body-schema fields:
- Body zones: face/head, hand/arm, trunk, leg/foot.
- Egocentric zones: near body, left peripersonal, right peripersonal, far/action space.
- Main afferents: S1 somatosensory integration, MT dorsal motion stream, vestibulo-parietal spatial input, pulvinar parietal attention, retrosplenial spatial reference transforms.
- Main efferents: PPC to SMA spatial-to-motor planning, PPC/PFC frontoparietal loop, PPC/ACC attention conflict.

Biological rule: simulator state may provide posture/contact/visual facts only; body schema must be formed by PPC spikes receiving through the connectome.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 5-20 ms (when this structure participates in feedback pathways).
