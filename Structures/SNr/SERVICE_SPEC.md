# SNr Service Specification

Biological justification: SNr output neurons are tonically active inhibitory gates suited to LIF tonic firing.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 8-12 ms (when this structure participates in feedback pathways).
