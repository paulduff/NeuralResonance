# PeriaqueductalGray Service Specification

Biological justification: Periaqueductal gray coordinates threat, pain, and defensive action programs through neuromodulator-sensitive pattern selection; dopamine-modulated STDP supports context-weighted gating.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: Izhikevich
- Plasticity: DopamineModulatedSTDP
- Feedback delay window: 3-10 ms (when this structure participates in feedback pathways).
