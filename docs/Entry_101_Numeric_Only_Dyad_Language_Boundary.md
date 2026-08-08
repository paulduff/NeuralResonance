# Entry 101 - Numeric-Only Dyad Language Boundary

Date: 2026-08-08

## Decision

Dyad language grounding may contain measured neuronal population evidence, but
it may not contain a host-authored name for what those populations mean.
Entity remains the trained language generator. DNNE remains the embodied
grounding and emission gate.

The boundary is now:

measured neuronal populations -> numeric evidence prompt -> Entity candidate -> bound neuronal review

## Removed Semantic Shortcut

The post-percept language annotation queue and
TryAttachLanguageAnnotation API have been deleted. Host code can no longer
attach an object identifier or label to a dominant percept ensemble.
NeuronalLanguageGroundingDecision therefore has no GroundedLabel, and its
provenance cannot include a post-percept annotation.

The Dyad grounding contract no longer contains:

- symbolic goal, semantic focus, need, or affect strings;
- a host communication-intent summary;
- prose memory excerpts;
- a grounded object label;
- DNNE fallback text or a fallback-used flag.

Protocol dyad.language-candidate.v2 reports only population identifiers,
confidence, circuit coverage, comprehension and expression drives,
uncertainty, speech authorization, and measured source provenance.

## No Synthetic Narration

The control program no longer turns a grounded label into
brainBehavior.language.utterance. AvatarBrainNarration, its JSON parser, and
the maze/world narration displays have been deleted. A simulator cannot
mistake a host label for speech produced by the distributed brain.

If Entity is unavailable, sleeping is observed, or a candidate is invalid,
the generation route returns a deferred response with no text. There is no
DNNE-authored fallback sentence.

## Prompt-Bound Grounding

Each issued Entity prompt now retains the exact neuronal grounding snapshot
used to create it. Candidate review uses that snapshot and rechecks the live
state before emission. The current percept ensemble, recall ensemble,
language-attention population, wake state, grounding, and speech authorization
must still agree.

This closes a time-of-check/time-of-use defect where an ungrounded candidate
could otherwise borrow a different grounded state that appeared while Entity
was generating.

## Entity Console

The Entity Blazor Dyad console now consumes protocol v2 and displays numeric
neuronal evidence. Goal, focus, need, affect, communication-intent, and prose
memory panels have been replaced with percept, recall, attention, coverage,
drive, uncertainty, and source-provenance measurements.

## Scientific Boundary

Population identifiers are not words and are not assumed to have fixed human
meaning. Entity may describe a candidate response, but DNNE does not receive
that response as sensory input, memory content, reward, goal, or motor command.
Text emission is an externally observable language act gated by neuronal
evidence; it is not a shortcut back into the brain.
