# SpinalCordMotor Service Specification

Biological justification: Spinal motor pools require rapid premotor integration and reflex-loop timing, which is represented by Izhikevich spiking with timing-dependent strengthening of effective motor synergies.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to preserve low-latency spike transport with explicit conduction-delay metadata.
- Inbound queue split: feed-forward queue for hierarchy-concordant flow and feedback queue for is_feedback=true traffic.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 1-4 ms (when this structure participates in feedback pathways).
