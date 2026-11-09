# Spindle implementation source review — 9 November 2026

Before replacing Avatar's frame-driven spindle inputs, the public
[Blum and Campbell source](https://github.com/kylepblum/MechanisticSpindleManuscript/tree/da0ed89078a948167b4e2b511480787ddb681892)
was downloaded and inspected without executing it. Its 16 files are pinned at
`da0ed89078a948167b4e2b511480787ddb681892`, the same revision linked by the
[2020 article](https://elifesciences.org/articles/55177). The ZIP SHA256 is
`F72A8E59744BBEB44AF712C2B3FD5617560FC6FBD2D39535ABD4352221A5D11D`.
The original GPL-3.0 license file is retained with the unmodified source in
`artifacts/spindle-source-20261109`. No upstream code was added to the runtime.

## What is available

The source implements bag/chain intrafusal cross-bridge distributions, a
muscle-tendon model, slack handling and force/yank conversion. The article uses
a modified Connor–Stevens conductance model for its passive-firing illustration.
This GitHub snapshot has no Simulink `.mdl` or `.slx` model: its active examples
instead contain threshold/reset integration. Therefore the downloaded repository
alone is not a complete, verified conductance-based Ia afferent implementation.
The linked Dryad dataset has not been inspected; no claim is made that a missing
membrane model cannot exist there or elsewhere.

The implemented mechanics remain useful source material, but require inspection
and qualification before adoption. A physiological idea, its equations, its
published implementation and a live causal port are distinct things.

## Concrete source findings

`source-audit.json` records original file hashes and line anchors for these
findings. They are static observations, not results of an executed model:

* `sarc2spindle.m` computes `diff(force)/diff(time)` and appends the last slope.
  The resulting index uses the next force sample. A live port must associate
  this with the completed interval and explicitly define causality; it cannot
  apply a future-dependent slope at the earlier neural timestamp.
* Both half-sarcomere classes initialize a detached amount before an ODE call,
  then use that cached amount inside the derivative instead of the solver's
  detached state. The final detached state is not copied back. Bound fraction
  is updated before evolution, and shifting follows evolution. Preserve these
  facts when reproducing the source; changing the population bookkeeping is a
  separate mechanistic candidate, not a faithful transcription.
* `find_hsl_from_force.m` shifts attached populations as the trial length varies,
  but its passive stress uses the old half-sarcomere length. At the declared
  initial zero-attachment state, both fibers have positive passive stress and
  the target-zero objective is constant in the trial displacement. Thus that
  initial objective has no finite root. The driver calls it before the first
  evolution. This source-level inconsistency needs an explicit reproduction
  and correction study, not a silent patch to the preserved reference.
* The Figure 9 example resets voltage after crossing its threshold. It is not
  the conductance-based membrane described for Figure 3 and must not be labelled
  a verified neuronal replacement just because it emits spike times.

These findings do not invalidate the experimental observations or establish
anything about a different author implementation. They prevent treating this
specific download as a ready-to-use biological reference.

## Avatar integration implications

Avatar already retains signed normalized muscle length and velocity. Its current
`AvatarMuscle` force is an extrafusal, activation/fatigue-based body force.
It must not be substituted directly for intrafusal fiber stress or interpreted
as a measured spindle receptor current. Muscle reference length, intrafusal
geometry and static/dynamic fusimotor drive need an explicit mapping. Preserve
the existing body mechanics while establishing those mechanisms separately.

The next spindle work must obtain reproducible mechanics and an explicitly
sourced afferent membrane, preserve original implementations, and test any
corrections separately. Then qualify force/length history, shortening and slack,
fusimotor effects, numerical refinement and actual membrane-crossing events.
The new physical-sample transport supplies timing and original observations;
it does not supply these physiological mechanisms.

No test, model or service ran during this inspection. No live Avatar, Unity,
editor, save or neuronal parameter changed. This source review does not change
the agreed CUDA timing → motor scheduling → gated motor-unit execution order.
