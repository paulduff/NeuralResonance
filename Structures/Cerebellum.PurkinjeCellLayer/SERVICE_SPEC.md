# Cerebellum.PurkinjeCellLayer Service Specification

Biological justification: Purkinje dendritic integration and complex-spike learning require HH-like membrane dynamics with climbing-fiber coupling.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: HH
- Plasticity: CerebellarLTD
- Feedback delay window: 10-15 ms (when this structure participates in feedback pathways).
