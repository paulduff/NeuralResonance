# Folded Archive Entry 105: Dyad Population-Language Adapter

## Decision

The first learned bridge from DNNE population state to Entity language lives on
the Entity side of the Dyad boundary. DNNE sends a numeric neuronal snapshot;
Entity converts it into a bounded residual bias over token logits. DNNE still
owns grounding, candidate review, and speech authorization.

## Numeric Contract

The adapter input contains:

- population indices for percept, recall, and attention;
- confidence, uncertainty, language attention, coverage, and drive values;
- sleep, circuit-observed, grounding-available, grounded, and speech gates;
- numeric source population indices, confidence, and sample ticks.

The adapter transport deliberately omits `SourceId`, `Evidence`, labels, text,
memory prose, named goals, and host interpretations. The existing human-readable
prompt remains inspectable context for Entity, but it is not parsed by the
adapter.

## Learned Component

Entity's fixed 32-value feature encoder feeds a trainable tanh latent layer and
a vocabulary projection. The resulting vector is clipped and scaled before it
is added to the base model's logits. This is computationally small enough for
the current laptop and does not modify old checkpoint formats. Adapter artifacts
are separately versioned, validated, atomically saved, and cached by file
version.

The initial trainer learns population-to-token residual distributions. It is a
real differentiable and persisted learning path, but not yet a sequence-level
translator. The base language model remains responsible for grammar and token
order.

## Runtime Configuration

Set `NRE_ENTITY_DYAD_ADAPTER_PATH` to an adapter file on the Entity host and
optionally set `NRE_ENTITY_DYAD_ADAPTER_STRENGTH` from `0` to `1` (default
`0.35`). DNNE sends structured numeric grounding on every Entity generation
request. Entity reports whether model generation actually used the adapter, and
DNNE records that status in the candidate configuration audit.

No adapter output can authorize an utterance. The exact generated candidate
must still pass DNNE's prompt-bound live neuronal review before Entry 104's
accepted-only presenter can display or voice it.

## Qualification Requirement

Before enabling an adapter by default, compare conditioned and unconditioned
runs over held-out embodied episodes. Measure candidate quality, grounding
agreement, deferral rate, speech-gate integrity, perturbation sensitivity, and
whether performance survives unseen population combinations.
