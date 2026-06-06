# Striatum Service Specification

Biological justification: MSN up/down state dynamics and action gating are represented with slowly integrating inhibitory LIF populations under dopamine control.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: DopamineModulatedSTDP
- Feedback delay window: 8-12 ms (when this structure participates in feedback pathways).
