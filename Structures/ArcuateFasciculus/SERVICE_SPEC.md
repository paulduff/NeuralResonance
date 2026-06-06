# ArcuateFasciculus Service Specification

Biological justification: Arcuate fasciculus acts as a temporally constrained dorsal white-matter relay between temporal comprehension and frontal production circuits.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 2-6 ms (when this structure participates in feedback pathways).
