# Entry 047 - Performance Harness

Date: 2026-05-30

## Purpose

The burn-in gate proves that the system stays alive. The performance harness measures how quickly it lives.

This entry adds `tools/perf-harness-dnne.ps1`, a repeatable sampler for the Control Program API. It records tick throughput, performance-snapshot and frame endpoint latency, tick-wall timing, acknowledgement latency, snapshot freshness, service health, frame payload size, and transport pressure.

The harness uses `/api/v1/performance/snapshot` by default so measurement does not repeatedly request the full diagnostics document. Use `-UseFullStateSnapshot` when intentionally measuring the heavier `/api/v1/state` path.

## How To Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\perf-harness-dnne.ps1 `
  -ControlBaseUrl http://localhost:5080 `
  -Profiles current `
  -WarmupSec 10 `
  -DurationSec 60 `
  -PollIntervalMs 500
```

To compare runtime profiles:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\perf-harness-dnne.ps1 `
  -Profiles normal,fast,headless `
  -WarmupSec 15 `
  -DurationSec 90
```

By default, profile changes are applied without simulation restart so the run is faster and less disruptive. Pass `-ApplyProfilesWithoutRestart:$false` when restart-inclusive timings are desired.

To save a known-good baseline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\perf-harness-dnne.ps1 `
  -Profiles normal,fast `
  -WarmupSec 15 `
  -DurationSec 90 `
  -BaselinePath tools\_perf-baseline.json `
  -SaveBaseline
```

To compare against that baseline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\perf-harness-dnne.ps1 `
  -Profiles normal,fast `
  -BaselinePath tools\_perf-baseline.json `
  -MaxRegressionPercent 12 `
  -FailOnRegression
```

The harness exits with code `2` when a failing threshold or failing regression gate is tripped.

## Artifacts

The harness writes:

- `tools/_perf-harness-summary.json`
- `tools/_perf-harness-samples.json`
- `tools/_perf-harness-summary.md`

Custom paths can be supplied with `-SummaryPath`, `-SamplesPath`, and `-MarkdownPath`.

## Metrics

Each profile run reports:

- Ticks per second from observed tick delta over measured wall time.
- Performance snapshot endpoint latency p50/p95/p99.
- Frame endpoint latency p50/p95/p99.
- Runtime tick-wall timing p50/p95/p99.
- Ack latency EWMA p50/p95/p99.
- Frame payload size approximation.
- Max non-OK services and max snapshot age.
- Transport deltas for generated, routed, delivered, dropped, and errored spikes.

Optional gates can assert:

- Minimum ticks per second.
- Maximum state/frame/tick-wall p95 latency.
- Maximum non-OK services and snapshot age.
- Maximum dropped spikes and dispatch errors.
- Maximum regression percentage against a saved baseline.

## Notes

The harness is intentionally separate from `burnin-dnne.ps1`. Burn-in is a pass/fail stability gate; this harness is a measurement instrument. It still produces valid output when the Control Program is offline, which keeps automation predictable and makes endpoint downtime visible as state/frame errors rather than a tool crash.

Baseline comparison is profile-aware. A `normal` run is compared only to the `normal` entry in the baseline summary, `fast` to `fast`, and so on.

The lightweight `/api/v1/performance/snapshot` route also backs `/api/v1/transport/stats`, preserving the older URL while exposing the richer metric set needed by the harness.
