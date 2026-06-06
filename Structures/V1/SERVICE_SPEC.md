# V1 Service Specification

Biological justification: Orientation selectivity in V1 emerges from nonlinear pyramidal responses with interneuron-mediated inhibition; BCM captures activity-dependent receptive-field stabilization.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: BCM
- Feedback delay window: 5-20 ms (when this structure participates in feedback pathways).
