# M1 Service Specification

Biological justification: Primary motor cortex M1, precentral gyrus/Brodmann area 4, carries a contralateral motor homunculus. Corticospinal layer V output and beta-rhythm motor coding require recurrent excitatory dynamics and inhibitory balance.

Homuncular body-map bands:
- Face/head: head turn, eye/mouth/neck orienting, scanning.
- Hand/arm: reach, grasp, tool/weapon handling.
- Trunk: posture, balance, torso stabilization.
- Leg/foot: locomotion, stride, turn, escape.

Biological rule: simulator state may provide body/sensory facts only; motor intent must arrive through M1-linked spikes and the connectome path into SpinalCordMotor.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: Izhikevich
- Plasticity: STDP
- Feedback delay window: 10-15 ms (when this structure participates in feedback pathways).
