# WernickePstgPsts Service Specification

Biological justification: Posterior superior temporal and superior temporal sulcus language areas support phonological-lexical decoding with recurrent cortical dynamics and cortical consolidation.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP+SynapticTaggingCapture
- Feedback delay window: 3-8 ms (when this structure participates in feedback pathways).
