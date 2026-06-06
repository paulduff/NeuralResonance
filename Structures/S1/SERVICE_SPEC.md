# S1 Service Specification

Biological justification: Primary somatosensory cortex S1, postcentral gyrus/Brodmann areas 3, 1, and 2, carries a contralateral sensory homunculus. RA/SA tactile and proprioceptive streams benefit from efficient LIF conductance integration with somatotopic inhibition.

Homuncular body-map bands:
- Face/head: head pose, mouth/eye/neck sensory feedback, orienting consequences.
- Hand/arm: touch, carried objects, tool/weapon contact.
- Trunk: posture, contact, balance, body pressure.
- Leg/foot: locomotor proprioception, ground contact, stride feedback.

Biological rule: simulator state may provide tactile/body facts only; body awareness must be carried by S1-linked spikes and routed onward through PPC/M1 by the connectome.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: LIF
- Plasticity: STDP
- Feedback delay window: 5-20 ms (when this structure participates in feedback pathways).
