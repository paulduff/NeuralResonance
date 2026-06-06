# NucleusTractusSolitarius Service Specification

Biological justification: Nucleus tractus solitarius encodes visceral afferents and autonomic set-point signals, so homeostatic gain modulation with low-latency LIF neurons captures baroreflex-like control.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: LIF
- Plasticity: HomeostaticGain
- Feedback delay window: 2-8 ms (when this structure participates in feedback pathways).
