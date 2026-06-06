# Subiculum Service Specification

Biological justification: Subiculum relay and place-related burst output can be abstracted as thresholded relay populations with adaptive firing.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 3-8 ms (when this structure participates in feedback pathways).
