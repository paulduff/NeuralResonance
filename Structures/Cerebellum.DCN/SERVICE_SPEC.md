# Cerebellum.DCN Service Specification

Biological justification: DCN rebound output after Purkinje inhibition is naturally represented by rebound-capable Izhikevich neurons.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 10-15 ms (when this structure participates in feedback pathways).
