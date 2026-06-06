# VestibularNuclei Service Specification

Biological justification: Vestibular nuclei require continuous head-motion integration and rapid reflex transfer, favoring stable LIF firing with timing-dependent adaptation for vestibulo-ocular calibration.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 1-5 ms (when this structure participates in feedback pathways).
