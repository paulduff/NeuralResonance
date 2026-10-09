# Asking Avatar — 9 October 2026

DNNE is the neuronal analogue of the brain, distributed across its separate nuclei. Avatar is its body and nervous system. WorldSim and MazeSim are environments. The embodied loop is:

**DNNE brain → Avatar nervous system and body → environment → Avatar senses → DNNE brain.**

Dyad and our own Entity model provide language interaction with this brain. CIV remains a standalone experiment outside this system.

## Operator interface

The main Blazor world view and the legacy WPF WorldSim/MazeSim diagnostics now have **Ask Avatar**. Presets ask about feeling, activity, thought and attention; the operator can also write a short question.

1. **Present question** renders the entire question as pixels using the existing shared renderer and retinal API. Unsupported glyphs and questions beyond the display's 120-character capacity are rejected rather than silently changed or truncated.
2. **Request reply** is a separate operation. It requires a later DNNE tick, forwards the operator's question as the existing Dyad generation purpose, and requests an Entity candidate using the current DNNE-issued evidence.
3. Only the exact candidate accepted for emission by DNNE is displayed as a reply. Sleeping, unavailable, ungrounded, unobserved, unauthorized, stale or mismatched reviews produce an empty reply area and an operator status message. Editing the question invalidates its receipt. Desktop clients also recheck endpoint changes after asynchronous requests.

The question string is kept on the client for turn association and supplied to Entity for language relevance. DNNE sensory ingress receives pixels, never interpreted words, semantic targets, motor commands or answers. Existing Entries 099, 100, 101 and 104 still apply. Entity is not permitted to feed its generated answer back into DNNE as perception, memory, reward or action.

The retinal route queues dispatch asynchronously. Its response and a later brain tick establish an opportunity for processing; they do not acknowledge neuronal delivery or prove question comprehension. The new receipt deliberately records this limitation. DNNE's candidate gate authorizes emission; the gate does not independently certify the meaning of each word.

## Language learning

There are three connected learning tasks:

- DNNE learns associations between sensory writing or speech, embodied events, actions, memory and internal neuronal states through its circuits and plasticity. For the current visual input, recognising writing is part of this work.
- Entity learns sentence formation and conversation in its own language weights.
- Entity's learned state encoder connects measured DNNE evidence to supported descriptions through Dyad. Real paired episodes, held out by complete episode, are needed to teach and evaluate this connection.

Training Entity alone does not teach DNNE to read or comprehend questions. Neuronal plasticity alone does not demonstrate successful language learning. Test both with unfamiliar presentations and changed experiences.

The active 9 October Entity run uses the existing 32-feature numeric grounding schema and measurement calibration fixtures. It does not yet expose enough affect, homeostatic, current-action or working-memory evidence to support reliable answers about feelings, activity or thoughts. Adding the interface does not close that gap. A later version must collect the relevant raw neuronal reports and corresponding verified embodied episodes, extend the feature schema explicitly, and test the learned descriptions. Population numbers must not acquire hard-coded human meanings. The active training package and its pinned runtime remain unchanged.

## Verification

The inquiry HTTP tests verify raw pixel transport, queued input handling, blocked/missing activity, later-tick requirements without stepping the brain, question/endpoint changes, exact accepted replies, stale accepted replies, invalid authorization, missing brain time and display bounds. All 55 selected inquiry, sensory, structured-language-authority and Dyad wire-contract tests pass. All three UI projects compile without warnings or errors. The live DNNE/world run is deliberately deferred until Entity training finishes.
