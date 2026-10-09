# Entity numeric-state voice integration — 9 October 2026

DNNE is the neuronal analogue of the brain and the authority, distributed across separate nuclei. Avatar is its body and nervous system. WorldSim and MazeSim are this project's environment simulations. The embodied loop is brain → Avatar → environment → Avatar → brain. Dyad and our own Entity language model provide interaction with that brain. CIV is a standalone experiment outside this path.

The [Ask Avatar interface](Avatar_Inquiry_2026-10-09.md) supplies a question/accepted-reply path while preserving neuronal sensory ingress and emission authority. The [first desktop run](Desktop_Brain_World_2026-10-09.md) is prepared for after Entity training finishes. Neither the interface nor Entity training establishes DNNE language comprehension on its own.

EntityLLM's new own-model candidate uses a learned numeric state encoder whose representations participate in decoder attention. It does not import third-party language weights. Its first training package contains measurement calibration fixtures, not verified external-scene or intention meanings. The training run must finish and its replies must be reviewed before choosing it as the configured voice.

The DNNE client now explicitly sends the requested compute backend to Entity's `/api/chat`. `NRE_ENTITY_COMPUTE_BACKEND` accepts `Auto`, `Cpu` or `Cuda`, defaulting to `Auto`; this avoids the API's managed-CPU default for TorchSharp-only checkpoints. Entity's API must be built with `-p:EntityCudaBackend=true` to install that optional backend and its native dependencies.

The client preserves candidate text and records optional returned architecture revision and numeric feature-schema metadata. Legacy responses remain compatible. `NRE_ENTITY_CHECKPOINT_PATH` still selects the actual saved model. For the new state-encoder checkpoint, `NRE_ENTITY_DYAD_ADAPTER_PATH` must be unset: the old vocabulary residual is a separate mechanism.

No authority or emission rules change. The prompt-bound numeric snapshot is still constructed by DNNE. Entity proposes language; DNNE performs the current-state review and either accepts the exact candidate or defers. Generated words do not update neuronal memory, reward, goals or motor decisions. Source identifiers remain provenance rather than word meanings.

Validation: the Entity HTTP-client mapping tests cover explicit backend selection and new optional provenance while preserving exact candidate text. Existing neuronal grounding, host-authority and Dyad wire-contract tests also pass. Entity's independent PowerShell rig separately qualifies CUDA and hosted inference before training its candidate.
