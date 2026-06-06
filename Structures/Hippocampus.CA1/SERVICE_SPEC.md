# Hippocampus.CA1 Service Specification

Biological justification: CA1 comparator behavior uses temporally sensitive pyramidal firing and longer-timescale consolidation mechanisms.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: SynapticTaggingCapture
- Feedback delay window: 3-8 ms (when this structure participates in feedback pathways).
