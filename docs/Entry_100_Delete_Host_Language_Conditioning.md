# Entry 100 - Delete Host Language Conditioning

Date: 2026-08-08

## Decision

DNNE no longer manufactures language activity from host-decoded perception.
The automatic perception-language bridge, adaptive host routing policy,
phonetic lexicon, English lexicon, and dialogue manager are deleted rather
than disabled.

The active sensory path is now:

`pixels or acoustic pressure -> sensory receptors -> neural connectome -> measured language populations`

No configuration switch can restore host-authored language spikes.

## Deleted Conditioning Loop

The former tick loop inspected visual-attention confidence, auditory firing
rate, and host-decoded percept labels. It converted those observations into
semantic tokens, chose comprehension, production, repetition, prosody, or
emergent mode, selected a hemisphere and fallback graph, then created spikes
whose neuron identifiers encoded lexical content. Although downstream
delivery used neural services, the activity's meaning and route were authored
by conventional code.

The following are physically absent:

- `InjectPerceptionLanguageConditioningAsync` and its dispatch helper;
- host percept-token and lexical-neuron builders;
- mode, hemisphere, reuptake, burst, and target selection helpers;
- `LanguageBackoffPolicy` and its adaptive graph state;
- `PhoneticLanguageEngine` and `EnglishLanguageLexicon`;
- `DialogueTurnManager` and host clarification state;
- related service registrations, settings, counters, logs, snapshots, editor
  panels, and administrative routes.

## Preserved Neural Language System

This deletion does not remove language anatomy or measured language output.
Broca BA44/45, Wernicke pSTG/pSTS, arcuate fasciculus,
supramarginal/angular, auditory association, fusiform, temporal association,
premotor, SMA, M1, thalamic, attention, memory, and prefrontal services remain
connected and trainable.

`NeuronalLanguageGroundingRuntime` remains a read-only decoder over measured
population activity. It may report circuit evidence but cannot inject spikes.
Passive brain narration remains observable output. Entity language candidates
remain subject to DNNE's neuronal grounding, attention, freshness, and
authority checks; conventional telemetry cannot replace missing neural
evidence.

## Removed Administrative Surface

The following routes now return `404 Not Found`:

- `/api/v1/admin/language/phonetics`;
- `/api/v1/admin/language/phonetics/generate`;
- `/api/v1/admin/language/phonetics/reset`;
- `/api/v1/admin/language/debug/backoff`;
- `/api/v1/admin/language/dialogue`;
- `/api/v1/admin/language/dialogue/reset`;
- `/api/v1/admin/language/prosody-telemetry`.

Transport telemetry now reports transport facts only. It no longer publishes
conditioning-spike or host language-routing statistics.

## Consequences

DNNE may appear less articulate until its language populations learn stable
associations through visual, auditory, embodied, memory, and social training.
That is an expected scientific result, not a runtime fault. A fluent host
lexicon would conceal whether the distributed neural system learned language.

Future language work must strengthen receptor-to-language pathways, temporal
binding, cross-modal grounding, plasticity, curriculum exposure, lesion tests,
and measured decoding. It must not reintroduce semantic ingress or direct
lexical spikes.

## Verification

Automated tests and source-boundary checks verify:

- all host conditioning classes and builders are absent;
- all host language authority routes return `404 Not Found`;
- no bridge setting or transport field remains;
- the editor no longer parses or displays bridge/backoff telemetry;
- structured typed language remains absent from the sensory API;
- neuronal language structures and their integration tests remain intact;
- the full distributed solution builds and the cortical benchmark remains
  independently measurable.
