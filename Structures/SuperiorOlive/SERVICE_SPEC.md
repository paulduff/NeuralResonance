# SuperiorOlive Service Specification

Biological justification: Superior olive performs binaural coincidence and interaural timing/amplitude comparison, which is well approximated by low-latency LIF coincidence detectors with STDP refinement.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 1-4 ms (when this structure participates in feedback pathways).
