# Retina Service Specification

Biological justification: Retinal ganglion output combines graded photoreceptor adaptation with spike transfer through center-surround circuits, so a membrane-rich HH profile with BCM-like adaptation best matches retinal coding.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: HH
- Plasticity: BCM
- Feedback delay window: 1-4 ms (when this structure participates in feedback pathways).
