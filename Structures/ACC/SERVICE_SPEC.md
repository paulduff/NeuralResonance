# ACC Service Specification

Biological justification: Conflict monitoring relies on rapid error-related burst coding and recurrent control signals.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 0-0 ms (when this structure participates in feedback pathways).
