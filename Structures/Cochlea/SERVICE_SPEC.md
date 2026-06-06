# Cochlea Service Specification

Biological justification: Cochlear transduction emphasizes phase-locked spike timing across tonotopic fibers, making fast LIF membranes with timing-sensitive STDP appropriate for auditory nerve-like drive.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 1-3 ms (when this structure participates in feedback pathways).
