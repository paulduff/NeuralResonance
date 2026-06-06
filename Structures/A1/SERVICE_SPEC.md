# A1 Service Specification

Biological justification: A1 onset/sustained channels require adaptable spike-frequency response; Izhikevich neurons represent temporal precision and adaptation.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 5-20 ms (when this structure participates in feedback pathways).
