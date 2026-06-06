# SupramarginalAngular Service Specification

Biological justification: Supramarginal and angular gyri integrate phonology, orthography, and multimodal semantic context during language comprehension.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP+SynapticTaggingCapture
- Feedback delay window: 4-10 ms (when this structure participates in feedback pathways).
