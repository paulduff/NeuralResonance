# Entry 081 - Neuronal Language Ingress

Date: 2026-08-06

## Purpose

This rung turns hosted text input into a sensory transducer rather than a
second symbolic language brain. The control program may convert text into
stable lexical and phonetic spike patterns, but it may not infer a command,
goal, reward, correction, memory write, or spoken response from the text.

## Removed Live Behavior

Language ingress no longer:

- analyzes English grammar or builds controller-authored intent features;
- resolves text to a command or motor directive;
- creates or reinforces a scalar language-intent record;
- recognizes teaching, reward, or correction keywords;
- writes semantic, episodic, place, or dopamine records from those keywords;
- returns legacy language intent, teaching-loop, or brain-narration records;
- falls back to scalar workspace, speech-intention, or conventional memory
  excerpts when the neuronal language circuit is unavailable.

The symbolic grammar and command records, parser methods, teaching-event
writer, public language-intent writer/getters, scalar Dyad fallback builders,
and teaching-loop endpoint are deleted. Tests that depended on injecting
symbolic teaching events are deleted with that interface.

## Authoritative Flow

Text is normalized into surface and phoneme tokens and dispatched as spikes to
the configured auditory-language pathway. Target neuron identifiers include a
stable bounded lexical key; they no longer contain a controller-assigned
semantic class such as command, need, threat, or memory.

Dyad candidate review always uses `NeuronalLanguageGroundingDecision`, even
when the decision reports that no circuit was observed. Missing neuronal
evidence therefore produces an explicit deferred, ungrounded result with no
fabricated memory excerpt. The runtime never restores the old scalar path.

## Boundary

Tokenization and phonetic conversion are input-device functions analogous to
retinal or cochlear preprocessing. They describe the stimulus; they do not
decide what it means. Grounding, attention selection, recall binding, and
speech authorization remain properties of measured neuronal population state.

Some conventional language, teaching, narration, and self-model records still
exist in checkpoint and diagnostics compatibility structures. Their live
language writer, public route, and Dyad fallback roots are now absent. They are
removed in subsequent compile-driven storage rungs.

