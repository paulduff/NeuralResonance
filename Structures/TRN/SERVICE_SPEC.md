# TRN Service Specification

Biological justification: TRN is predominantly inhibitory and fast-gating; LIF captures efficient GABAergic gating and spotlight competition.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 0-0 ms (when this structure participates in feedback pathways).
