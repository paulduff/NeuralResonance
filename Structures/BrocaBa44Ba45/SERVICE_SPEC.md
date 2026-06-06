# BrocaBa44Ba45 Service Specification

Biological justification: Inferior frontal BA44/45 sequencing and articulatory planning require persistent excitatory activity and dopamine-sensitive gating.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: DopamineModulatedSTDP+SynapticTaggingCapture
- Feedback delay window: 5-12 ms (when this structure participates in feedback pathways).
