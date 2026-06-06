# SuperiorColliculus Service Specification

Biological justification: Persistent activity, D1/D2 gating, and flexible control are captured with recurrent Izhikevich cells plus dopamine-modulated learning.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: DopamineModulatedSTDP+SynapticTaggingCapture
- Feedback delay window: 5-20 ms (when this structure participates in feedback pathways).

