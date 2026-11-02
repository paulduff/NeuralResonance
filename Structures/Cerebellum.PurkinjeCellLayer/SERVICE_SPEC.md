# Cerebellum.PurkinjeCellLayer Service Specification

Biological candidate: local mGluR/IP3, voltage-dependent calcium entry, finite internal calcium stores and slow calcium-dependent LTD replace the direct climbing-event weight decrement. Climbing fibres drive an explicit receiving AMPA conductance compartment. The membrane remains reduced; this does not qualify full complex spikes, human physiology, LTP or balance. See `docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md` for parameters and limits.

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for is_feedback=true messages.
- Neuron model: HH
- Plasticity: PurkinjeCalciumLTD
- Save fingerprint: includes the calcium and climbing-compartment versions. Older saves are preserved and rejected, never silently migrated. New saves are learned-weight warm starts; chemistry is reinitialized.
- Feedback delay window: 10-15 ms (when this structure participates in feedback pathways).
