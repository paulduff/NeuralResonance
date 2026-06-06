# ReticularFormation Service Specification

Biological justification: Reticular formation mixes arousal, posture, and premotor gating with broad recurrent motifs; Izhikevich neurons with homeostatic gain reflect state-dependent tonic-burst transitions.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: Izhikevich
- Plasticity: HomeostaticGain
- Feedback delay window: 2-10 ms (when this structure participates in feedback pathways).
