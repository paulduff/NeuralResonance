# Full slow population initialization review

Paul reported final completion of `muscle-population-initial-20261123-370cb9`.
Its frozen source is `917be280abd608f3b33910028cabb0d180de4234`. Both complete
2,913-file copies and manifest, 2,268 Git files, declared executables/runtime,
379 exact managed cases and eight packaging controls are retained. The six-file
review in `artifacts/muscle-population-initial-20261123-370cb9-review` has its
own verified D: backup. Review executed no tests or models.

All four complete 147-unit initial populations agree with 80/110-digit passive
fiber/tendon balances under the original criteria. The eight earlier equilibrium
controls also pass. Every unit begins with zero calcium, activation and arrivals;
the original measured body timestamp and all source parameters are preserved.

| Side and dimensions | Resting fiber / optimal | Tendon tension (N) |
|---|---:|---:|
| Left, original reference | 0.4739525522 | 0.0000566927 |
| Right, original reference | 0.4926938702 | 0.0000813095 |
| Left, source dimension candidate | 1.0039449756 | 1.512867062 |
| Right, source dimension candidate | 1.0039468557 | 1.512921732 |

The candidate combines the declared mapped anatomical lengths with the existing
TA force parameters, constant pennation and preserved reference cross-section.
It is a dimensional candidate, not anatomical calibration. This result supports
testing its active dynamics; it does not establish recruitment, useful joint
control or standing. The 53 fast units remain excluded because their dynamics
are not implemented and qualified in this slow-fiber model.

Next: preserve these exact initial states in an eight-condition, three-resolution
active study, with all 147 independent slow-unit states, the shared mechanical
fiber/tendon state and independent numerical references. Diagnostic arrivals
must be explicitly labelled; they do not demonstrate neuronal authority.
