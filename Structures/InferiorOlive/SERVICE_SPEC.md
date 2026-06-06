# InferiorOlive Service Specification

Biological justification: Inferior olive subthreshold oscillation and synchronized complex spikes require conductance-based dynamics.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: HH
- Plasticity: STDP
- Feedback delay window: 10-15 ms (when this structure participates in feedback pathways).
