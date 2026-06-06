# Hippocampus.CA3 Service Specification

Biological justification: CA3 autoassociation and recurrent bursting require nonlinear spiking with recurrent collateral support.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: MossyFiberLTP
- Feedback delay window: 2-5 ms (when this structure participates in feedback pathways).
