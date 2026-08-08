# Folded Archive Entry 107: Accepted Neuronal Language Episodes

## Purpose

The learned Dyad population-language adapter needs reviewed examples without
restoring symbolic cognition. This rung turns DNNE's existing prompt-bound
language review into a narrow curriculum boundary.

## Export Boundary

`GET /api/v1/dyad/language/adapter-training?limit=256` returns only reviews
whose exact candidate text was accepted for emission while the neuronal
language circuit was observed, grounded, awake, and speech-authorized.

Each record contains:

- the exact accepted target text;
- review sequence and time;
- domain-separated SHA-256 session, turn, and candidate fingerprints;
- numeric population indices, confidence, timing, coverage, drive,
  uncertainty, and gate values.

It deliberately excludes raw session and turn IDs, prompts, decision reasons,
authority strings, source IDs, evidence prose, and source references. Deferred
candidates never enter the feed. Population `-1` diagnostic placeholders are
filtered rather than presented as neuronal evidence.

## Collection

With the DNNE control program running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\export-dyad-adapter-training.ps1" -ControlProgramUrl "http://localhost:5080" -OutputPath "C:\Users\User\source\repos\EntityLLM\artifacts\dyad-adapter-training\accepted.jsonl"
```

The script validates every returned gate and writes JSONL through a temporary
file. The default output is under the ignored DNNE `artifacts` directory.

## Scientific Limit

Acceptance indicates that the candidate was authorized by the observed
neuronal state; it does not establish that the sentence is objectively true or
that DNNE generated its syntax. Dataset growth must remain reviewed, diverse,
and measurable. Entity still proposes wording, and every future emitted phrase
still requires a fresh DNNE neuronal review.
