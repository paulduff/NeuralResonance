# InferiorColliculus Service Specification

Biological justification: Inferior colliculus integrates ascending auditory streams with multimodal orienting inputs; Izhikevich neurons preserve burst/tonic transitions needed for salience coding.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 2-6 ms (when this structure participates in feedback pathways).
