# Entry 057 - Dyad Language Boundary

## Purpose

Dyad is the collaboration between Entity, the language-facing model, and DNNE, the grounded embodied system. This first boundary gives Entity a narrow way to submit a language candidate without giving it control over DNNE.

It implements the first executable portion of the boundary described in Entries 055 and 056. Those entries remain the governing roadmap and public-research constraints.

## Contract

The control program exposes two authenticated control-plane endpoints:

- `POST /api/v1/dyad/language/candidates`
- `GET /api/v1/dyad/language/reviews`

The current protocol version is `dyad.language-candidate.v1`. A candidate must identify its session, turn, Entity version, configuration marker, prompt fingerprint, kind, text, and optional source references. Candidate kinds are restricted to `utterance`, `interpretation`, `question`, and `dialogue`.

The candidate schema deliberately contains no motor directive, reward value, memory operation, world-state assertion, or direct stimulation field. Entity cannot use this route to alter DNNE's body, reward, memory, or action selection.

## DNNE Authority

DNNE independently provides a compact grounding snapshot with sleep state, language-workspace state, attention, goal/need context, and the existing speech gate. The first gate is conservative:

- sleeping DNNE defers the candidate;
- an inactive or low-confidence language workspace defers it;
- a closed DNNE speech gate defers it;
- only an independently open DNNE speech gate yields `AcceptedForReview`.

`AcceptedForReview` is not a command to speak and does not emit motor spikes or write a memory. It only marks the proposal as eligible for the next, separately implemented, grounded utterance stage.

Every valid proposal is retained in a bounded, read-only review feed with Entity version/configuration, prompt fingerprint, candidate text, decision, reason, timestamp, and grounding snapshot. This makes later episode-artifact export possible without treating Entity output as world truth.

## Entity Adapter

The control program now owns an opt-in adapter at `POST /api/v1/dyad/language/generate`. It builds a bounded prompt from DNNE's verified internal state, asks Entity through its existing hosted `/api/chat` endpoint, validates the result with the candidate contract, and then applies the same DNNE review gate.

The adapter is disabled unless `NRE_ENTITY_ENABLED=true` and `NRE_ENTITY_CHECKPOINT_PATH` is configured. `NRE_ENTITY_API_URL` defaults to `http://127.0.0.1:5165`; `NRE_ENTITY_API_KEY` supplies the Entity API's `X-Entity-Api-Key` credential. Optional `NRE_ENTITY_CHAT_EXAMPLES_PATH`, `NRE_ENTITY_IDENTITY_PROFILE_PATH`, `NRE_ENTITY_HISTORY_PATH`, and `NRE_ENTITY_KNOWLEDGE_PATH` select resources on the machine hosting Entity. These paths are Entity-host paths, not DNNE-host paths when the services run on different computers. Generation settings can be configured with `NRE_ENTITY_TOKENS`, `NRE_ENTITY_TEMPERATURE`, `NRE_ENTITY_TOP_K`, `NRE_ENTITY_SEED`, and `NRE_ENTITY_TIMEOUT_MS`.

The adapter is explicitly invoked and is not part of the simulation tick. A sleeping DNNE, disabled bridge, Entity outage, malformed reply, or contract failure returns DNNE's own existing narration as `dnne-fallback`; the simulation continues and Entity cannot take an action through failure handling.

## Grounded Context Extension

The prompt now includes a compact, read-only context assembled by DNNE under the same lock as the grounding snapshot:

- a prefrontal working-memory excerpt is always present;
- episodic, semantic, and place-memory excerpts are included when DNNE has them;
- a communication-intent snapshot describes expression only: intent, mood, subject, strength, and evidence.

Each excerpt is bounded, labelled with its originating memory system, confidence, last-updated tick, and evidence. The prompt explicitly describes these as DNNE internal reports rather than proof of external events. The communication-intent snapshot deliberately excludes `MotorDirective`, and it is not a request to change action selection, reward, or memory.

The complete structured grounding snapshot is retained alongside every review record. This preserves the exact read-only context available at the language boundary without making Entity an authority over that context.

## Embodied Launch

`tools/start-dyad.ps1` starts the DNNE control program with the Entity bridge configured and then starts exactly one simulator: `World`, `Maze`, or `None`. It does not connect Entity to a simulator. The runtime flow remains:

`brain -> avatar -> simulation -> avatar -> brain`

For example, the World simulation can be launched with:

```powershell
.\tools\start-dyad.ps1 -Simulator World -EntityCheckpointPath 'C:\path\to\entity-checkpoint.json'
```

The selected simulator receives motor dispatches through `NRE.SimAvatar` and returns body, outcome, and sensory inputs through the same avatar boundary. The helper does not start the unrelated NeuralCivilisationSim project.

## Next Step

Exercise the adapter against a recorded deterministic benchmark episode, including Entity-enabled, Entity-unavailable, malformed-output, and DNNE-only fallback conditions. The replay artifact must retain the prompt, structured grounding snapshot, candidate, review decision, and the independently recorded DNNE action trace. No candidate may progress beyond review until those comparisons are reproducible.
