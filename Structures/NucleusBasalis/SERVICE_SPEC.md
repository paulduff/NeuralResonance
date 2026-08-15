# BasalForebrainCholinergic Service Specification

Biological justification: Cholinergic transients and attention gating are modeled as burst-capable modulatory LIF outputs.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: HomeostaticGain
- Feedback delay window: 0-0 ms (when this structure participates in feedback pathways).
