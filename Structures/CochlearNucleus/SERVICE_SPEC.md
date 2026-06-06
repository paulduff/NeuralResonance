# CochlearNucleus Service Specification

Biological justification: Cochlear nucleus populations mix onset, chopper, and sustained response motifs; Izhikevich dynamics capture this heterogeneity while preserving millisecond timing plasticity.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 1-5 ms (when this structure participates in feedback pathways).
