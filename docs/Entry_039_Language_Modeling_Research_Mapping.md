# Entry 039 - Language Modeling Research Mapping

## Scope
This entry maps two papers to concrete implementation steps in NeuralResonanceEngine.DNN language pathways.

- Paper A: *Automatic Learning of Language Model Structure* (COLING 2004, C04-1022)
- Paper B: *Dialogue Systems and Conversational Agents for Patients with Dementia: The Human-Robot Interaction* (Rejuvenation Research, 2018)

## Why These Papers Matter Here

### Paper A (C04-1022)
Key contribution: structured, data-driven search over language model structure (conditioning factors, backoff graph, discounting), especially useful under sparse data and rich morphology.

Relevance to DNNE:
- We already have emergent lexicalization and language modes.
- We can upgrade from fixed hand-tuned routing/conditioning to adaptive structure selection based on observed transport and pathway outcomes.

### Paper B (Dialogue systems review, 2018)
Key contribution: end-to-end spoken dialogue concerns (ASR/NLU/DM/NLG loop), robustness, and interaction constraints in vulnerable populations.

Relevance to DNNE:
- We now have microphone + webcam feeds and language stimulus injection.
- We need a stronger dialogue loop manager and graceful degradation under uncertainty/timeouts.

## Architecture Mapping (Paper -> Component)

### A. Factorized Language Modeling (from C04-1022)
Map to:
- `ControlProgram/Program.cs` (`PhoneticLanguageEngine`)
- Language stimulus APIs:
  - `/api/v1/admin/input/language`
  - `/api/v1/admin/language/phonetics/*`

Implementation direction:
1. **Explicit factor vectors per lexeme**
   - Add orthogonal factors: phonology, morphology-lite, syntactic role hint, semantic cluster id.
   - Keep surface form separate from factor representation.
2. **Backoff graph over factors**
   - Replace single fallback chain with a scored backoff graph.
   - Evaluate candidate paths per tick using success/delivery/sparsity.
3. **Data-driven structure search**
   - Lightweight online search (bandit or GA-lite) over candidate conditioning sets.
   - Objective: maximize delivered spikes to language pathways while minimizing dispatch failures and dead paths.
4. **Discounting/normalization controls**
   - Add tunable discount profile for low-frequency lexemes and rare factor combos.

### B. Dialogue Loop Robustness (from 2018 review)
Map to:
- `src/NRE.WpfEditor/MainWindow.xaml(.cs)`
- `ControlProgram/Program.cs` input APIs and telemetry logs

Implementation direction:
1. **Turn Manager state machine**
   - States: Listen -> Parse -> Plan -> Stimulate -> Observe -> Respond.
   - Track turn id and confidence.
2. **Clarification strategy**
   - If confidence below threshold, route to clarification utterance generation instead of direct production.
3. **Latency/error-aware fallback**
   - If service route is degraded, shift to conservative mode (`repetition`) and shorter stimulus plans.
4. **Session memory policy**
   - Keep small dialogue context window tied to ATP/sleep state and engram salience.

## Guardrails
- Keep biology-first constraints:
  - no direct cross-service calls
  - spike messages remain boundary contract
  - neuromod levels continue to bias behavior globally
- Keep performance constraints:
  - avoid heavy global optimization each tick
  - update structure policy only at coarse intervals

## Success Metrics
- Increased `activePathways` in language routes without raising non-OK services.
- Higher spontaneous + task-driven `dispatched` spikes reaching language targets.
- Lower dispatch error rate and fewer fallback-to-timeout events.
- Stable frame/render behavior in editor while language activity increases.

## Sources
- ACL Anthology C04-1022: https://aclanthology.org/C04-1022/
- ACL PDF: https://aclanthology.org/C04-1022.pdf
- PubMed (2018 dialogue systems review): https://pubmed.ncbi.nlm.nih.gov/30033861/
